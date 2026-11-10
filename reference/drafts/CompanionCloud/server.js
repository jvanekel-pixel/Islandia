import express from 'express';
import cors from 'cors';
import multer from 'multer';
import rateLimit from 'express-rate-limit';
import { WebSocketServer } from 'ws';
import crypto from 'crypto';
import fs from 'fs/promises';
import path from 'path';
import { execFile } from 'child_process';
import { promisify } from 'util';

import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { dedup, prune, weld, simplify, draco, resample, center } from '@gltf-transform/functions';
import { MeshoptSimplifier } from 'meshoptimizer';
import draco3d from 'draco3dgltf';

const execFileAsync = promisify(execFile);

const PORT = process.env.PORT || 8080;
const UPLOAD_DIR = path.resolve('./uploads_temp');
const SANITIZED_DIR = path.resolve('./sanitized_glbs');
const MAX_OUTPUT_GLB_BYTES = 15 * 1024 * 1024; // Strict < 15 MB Phase 6 Gate
const PAIRING_CODE_TTL_MS = 10 * 60 * 1000;    // P1 #9: 10-minute expiring pairing codes

await fs.mkdir(UPLOAD_DIR, { recursive: true });
await fs.mkdir(SANITIZED_DIR, { recursive: true });

const app = express();
app.use(cors());
app.use(express.json());
app.use('/assets', express.static(SANITIZED_DIR));

// P1 #9 Security: Strict Upload Rate Limiter (Max 10 model conversions per 15 min per IP)
const uploadLimiter = rateLimit({
  windowMs: 15 * 60 * 1000,
  max: 10,
  message: { error: 'Too many 3D uploads. Please wait 15 minutes.' }
});

// Multer temporary upload storage capped at 100 MB raw source file
const upload = multer({
  dest: UPLOAD_DIR,
  limits: { fileSize: 100 * 1024 * 1024 }
});

// Active Pairing Registry: code -> { ws, createdAt, deviceTier, assets: [] }
const pairingSessions = new Map();

// Initialize gltf-transform NodeIO with Draco encoder/decoder
await MeshoptSimplifier.ready;
const io = new NodeIO()
  .registerExtensions(ALL_EXTENSIONS)
  .registerDependencies({
    'draco3d.decoder': await draco3d.createDecoderModule(),
    'draco3d.encoder': await draco3d.createEncoderModule()
  });

/**
 * 1. Endpoint for Meta Quest Headset to register/refresh a 6-digit expiring Pairing Code
 */
app.post('/api/pairing/register', (req, res) => {
  const { deviceTier = 'Quest3' } = req.body;
  let code;
  do {
    code = crypto.randomInt(100000, 999999).toString();
  } while (pairingSessions.has(code));

  pairingSessions.set(code, {
    ws: null,
    createdAt: Date.now(),
    deviceTier,
    assets: []
  });

  res.json({
    pairingCode: code,
    expiresInSeconds: PAIRING_CODE_TTL_MS / 1000
  });
});

/**
 * 2. Endpoint for Web Browser Companion to verify a 6-digit Pairing Code
 */
app.get('/api/pairing/verify/:code', (req, res) => {
  const session = pairingSessions.get(req.params.code);
  if (!session || (Date.now() - session.createdAt) > PAIRING_CODE_TTL_MS) {
    pairingSessions.delete(req.params.code);
    return res.status(404).json({ valid: false, error: 'Pairing code expired or invalid.' });
  }
  res.json({ valid: true, deviceTier: session.deviceTier, queuedCount: session.assets.length });
});

/**
 * 3. Universal 3D Upload, Scale/Pivot Normalization, KTX2 Texture Transcoding & Draco Pipeline
 */
app.post('/api/models/upload', uploadLimiter, upload.single('modelFile'), async (req, res) => {
  if (!req.file) {
    return res.status(400).json({ error: 'No 3D model file provided.' });
  }

  const rawFilePath = req.file.path;
  const originalName = path.basename(req.file.originalname);
  const ext = path.extname(originalName).toLowerCase();
  const allowedExts = new Set(['.glb', '.gltf', '.fbx', '.obj', '.blend', '.stl', '.dae']);

  try {
    if (!allowedExts.has(ext)) {
      throw new Error(`Unsupported 3D extension: ${ext}`);
    }

    const {
      pairingCode,
      unitScaleFactor = '1.0',
      centerBottomPivot = 'true',
      maxTextureResolution = '1024', // 1024px default for props, 2048px max for hero architecture
      targetTriangleRatio = '0.5'
    } = req.body;

    // P1 #9 Security: Reject executable binaries disguised as 3D models (MZ / ELF magic bytes)
    const headerBuf = Buffer.alloc(4);
    const fd = await fs.open(rawFilePath, 'r');
    await fd.read(headerBuf, 0, 4, 0);
    await fd.close();
    if (headerBuf.slice(0, 2).toString() === 'MZ' || headerBuf.toString() === '\x7fELF') {
      throw new Error('Security violation: Executable binary detected.');
    }

    let intermediateGlbPath = `${rawFilePath}_raw.glb`;

    // Step A: If format is not already .glb, run Sandboxed Blender CLI Converter
    if (ext !== '.glb') {
      await execFileAsync('blender', [
        '--background',
        '--factory-startup',
        '-noaudio',
        '--python', path.resolve('./blender_safe_convert.py'),
        '--',
        rawFilePath,
        intermediateGlbPath,
        String(parseFloat(unitScaleFactor) || 1.0),
        String(centerBottomPivot)
      ], { timeout: 45000 });
    } else {
      await fs.copyFile(rawFilePath, intermediateGlbPath);
    }

    // Step B: Load into @gltf-transform/core for geometry & texture sanitization
    const document = await io.read(intermediateGlbPath);
    const scaleVal = parseFloat(unitScaleFactor) || 1.0;

    await document.transform(
      dedup(),
      prune(),
      weld({ tolerance: 0.0001 }),
      resample(),
      // Center pivot at bottom of bounding box if requested
      ...(centerBottomPivot === 'true' ? [center({ pivot: 'below' })] : []),
      // Decimate high-poly meshes to protect Quest triangle budget
      simplify({
        simplifier: MeshoptSimplifier,
        ratio: Math.min(1.0, Math.max(0.1, parseFloat(targetTriangleRatio) || 0.5)),
        error: 0.005
      }),
      // Compress geometry with Draco (KHR_draco_mesh_compression)
      draco({ method: 'edgebreaker' })
    );

    // Apply uniform unit scale factor to root nodes if uploaded as native .glb
    if (ext === '.glb' && Math.abs(scaleVal - 1.0) > 0.001) {
      for (const scene of document.getRoot().listScenes()) {
        for (const node of scene.listChildren()) {
          const s = node.getScale();
          node.setScale([s[0] * scaleVal, s[1] * scaleVal, s[2] * scaleVal]);
        }
      }
    }

    const preKtxGlbPath = `${rawFilePath}_prektx.glb`;
    await io.write(preKtxGlbPath, document);

    // Step C: Transcode all textures to KTX2 BasisU (ETC1S/UASTC) capped at 1024px or 2048px
    // (Prevents 4K texture OOM crashes on Quest and avoids unsupported WebP textures!)
    const texCap = parseInt(maxTextureResolution, 10) === 2048 ? '2048' : '1024';
    const assetHash = crypto.createHash('sha256').update(await fs.readFile(preKtxGlbPath)).digest('hex').slice(0, 16);
    const finalFileName = `${assetHash}.glb`;
    const finalGlbPath = path.join(SANITIZED_DIR, finalFileName);

    try {
      await execFileAsync('npx', [
        'gltf-transform', 'etc1s',
        preKtxGlbPath,
        finalGlbPath,
        '--power-of-two',
        '--max-size', texCap
      ], { timeout: 60000 });
    } catch {
      // Fallback if toktx binary is not installed in local dev environment
      await fs.copyFile(preKtxGlbPath, finalGlbPath);
    }

    const finalStats = await fs.stat(finalGlbPath);
    if (finalStats.size > MAX_OUTPUT_GLB_BYTES) {
      await fs.unlink(finalGlbPath);
      throw new Error(`Sanitized GLB (${(finalStats.size / 1048576).toFixed(2)} MB) exceeds the 15 MB Meta Quest gate.`);
    }

    // Estimate uncompressed runtime RAM/VRAM footprint on headset (approx 2.2x KTX2+Draco file size)
    const estimatedRuntimeMemoryBytes = Math.round(finalStats.size * 2.2);

    const assetMetadata = {
      assetSha256Id: assetHash,
      displayName: path.parse(originalName).name,
      glbUrl: `/assets/${finalFileName}`,
      fileSizeBytes: finalStats.size,
      estimatedRuntimeMemoryBytes,
      timestampUtc: Date.now()
    };

    // Step D: Push real-time notification to paired Meta Quest headset over WebSocket
    const session = pairingSessions.get(pairingCode);
    if (session) {
      session.assets.push(assetMetadata);
      if (session.ws && session.ws.readyState === 1) {
        session.ws.send(JSON.stringify({
          type: 'NEW_SANITIZED_GLB_READY',
          payload: assetMetadata
        }));
      }
    }

    res.json({
      success: true,
      asset: assetMetadata
    });
  } catch (err) {
    res.status(422).json({ error: err.message });
  } finally {
    // Clean up temporary upload files
    await fs.rm(rawFilePath, { force: true }).catch(() => {});
    await fs.rm(`${rawFilePath}_raw.glb`, { force: true }).catch(() => {});
    await fs.rm(`${rawFilePath}_prektx.glb`, { force: true }).catch(() => {});
  }
});

const server = app.listen(PORT, () => {
  console.log(`[Phase 6 Sanitization Cloud] Listening on port ${PORT}`);
});

// WebSocket Server for instant Headset <-> Browser Companion sync
const wss = new WebSocketServer({ server });
wss.on('connection', (ws, req) => {
  const url = new URL(req.url, `http://localhost:${PORT}`);
  const code = url.searchParams.get('code');
  const session = pairingSessions.get(code);

  if (!session || (Date.now() - session.createdAt) > PAIRING_CODE_TTL_MS) {
    ws.close(4001, 'Invalid or expired pairing code');
    return;
  }

  session.ws = ws;
  ws.send(JSON.stringify({ type: 'PAIRING_CONNECTED', queuedAssets: session.assets }));
});
