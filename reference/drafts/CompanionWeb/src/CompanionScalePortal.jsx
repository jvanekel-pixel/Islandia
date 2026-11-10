import React, { useState, useRef, useEffect } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { OBJLoader } from 'three/examples/jsm/loaders/OBJLoader.js';
import { FBXLoader } from 'three/examples/jsm/loaders/FBXLoader.js';

export default function CompanionScalePortal({ cloudBaseUrl = 'http://localhost:8080' }) {
  const mountRef = useRef(null);
  const sceneRef = useRef(null);
  const loadedGroupRef = useRef(null);

  const [pairingCode, setPairingCode] = useState('');
  const [isPaired, setIsPaired] = useState(false);
  const [selectedFile, setSelectedFile] = useState(null);
  const [unitScale, setUnitScale] = useState(1.0);
  const [centerBottomPivot, setCenterBottomPivot] = useState(true);
  const [maxTexSize, setMaxTexSize] = useState('1024');
  const [modelStats, setModelStats] = useState({ triangles: 0, dimensionsMeters: [0, 0, 0] });
  const [uploadStatus, setUploadStatus] = useState('');

  // Initialize Three.js Viewport with 1.8m Human Reference Cage & 1m Grid
  useEffect(() => {
    const container = mountRef.current;
    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x111722);
    sceneRef.current = scene;

    const camera = new THREE.PerspectiveCamera(45, container.clientWidth / container.clientHeight, 0.05, 500);
    camera.position.set(3.2, 2.4, 4.2);

    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(container.clientWidth, container.clientHeight);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    container.appendChild(renderer.domElement);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.target.set(0, 0.9, 0);
    controls.update();

    // Lighting
    scene.add(new THREE.HemisphereLight(0xddeeff, 0x334455, 1.1));
    const dirLight = new THREE.DirectionalLight(0xffffff, 1.2);
    dirLight.position.set(5, 10, 7);
    scene.add(dirLight);

    // 1m x 1m Ground Calibration Grid (8m x 8m Chunk Reference)
    const grid = new THREE.GridHelper(8, 8, 0x00ff88, 0x334466);
    scene.add(grid);

    // 1.80m Human Reference Calibration Cage (Green wireframe cylinder/capsule at X = -1.2m)
    const humanGeo = new THREE.CylinderGeometry(0.22, 0.22, 1.80, 16);
    humanGeo.translate(-1.2, 0.90, 0);
    const humanMat = new THREE.MeshBasicMaterial({ color: 0x00ff88, wireframe: true });
    const humanRefMesh = new THREE.Mesh(humanGeo, humanMat);
    scene.add(humanRefMesh);

    const modelHolder = new THREE.Group();
    scene.add(modelHolder);
    loadedGroupRef.current = modelHolder;

    let animId;
    const animate = () => {
      animId = requestAnimationFrame(animate);
      renderer.render(scene, camera);
    };
    animate();

    return () => {
      cancelAnimationFrame(animId);
      renderer.dispose();
      container.innerHTML = '';
    };
  }, []);

  // Re-evaluate Scale & Bottom-Center Pivot whenever user changes unit presets
  useEffect(() => {
    const holder = loadedGroupRef.current;
    if (!holder || holder.children.length === 0) return;

    const model = holder.children[0];
    model.scale.setScalar(unitScale);
    model.position.set(0, 0, 0);
    model.updateMatrixWorld(true);

    const box = new THREE.Box3().setFromObject(model);
    const size = new THREE.Vector3();
    const center = new THREE.Vector3();
    box.getSize(size);
    box.getCenter(center);

    if (centerBottomPivot) {
      model.position.set(-center.x, -box.min.y, -center.z);
    }

    let tris = 0;
    model.traverse((child) => {
      if (child.isMesh && child.geometry) {
        const index = child.geometry.index;
        tris += index ? index.count / 3 : child.geometry.attributes.position.count / 3;
      }
    });

    setModelStats({
      triangles: Math.round(tris),
      dimensionsMeters: [size.x.toFixed(2), size.y.toFixed(2), size.z.toFixed(2)]
    });
  }, [unitScale, centerBottomPivot, selectedFile]);

  const handleFileSelect = (e) => {
    const file = e.target.files[0];
    if (!file) return;
    setSelectedFile(file);

    const ext = file.name.split('.').pop().toLowerCase();
    const url = URL.createObjectURL(file);
    const holder = loadedGroupRef.current;
    holder.clear();

    const onLoaded = (obj) => {
      holder.add(obj);
      URL.revokeObjectURL(url);
      setUnitScale(1.0);
    };

    if (ext === 'glb' || ext === 'gltf') {
      new GLTFLoader().load(url, (gltf) => onLoaded(gltf.scene));
    } else if (ext === 'obj') {
      new OBJLoader().load(url, onLoaded);
    } else if (ext === 'fbx') {
      new FBXLoader().load(url, onLoaded);
    }
  };

  const verifyPairingCode = async () => {
    const res = await fetch(`${cloudBaseUrl}/api/pairing/verify/${pairingCode}`);
    const data = await res.json();
    setIsPaired(Boolean(data.valid));
    setUploadStatus(data.valid ? `Paired with ${data.deviceTier}!` : data.error);
  };

  const handleSanitizeAndSendToQuest = async () => {
    if (!selectedFile || !isPaired) return;
    setUploadStatus('Sanitizing geometry, encoding KTX2 textures & compressing Draco...');

    const formData = new FormData();
    formData.append('modelFile', selectedFile);
    formData.append('pairingCode', pairingCode);
    formData.append('unitScaleFactor', String(unitScale));
    formData.append('centerBottomPivot', String(centerBottomPivot));
    formData.append('maxTextureResolution', maxTexSize);

    const res = await fetch(`${cloudBaseUrl}/api/models/upload`, {
      method: 'POST',
      body: formData
    });
    const data = await res.json();
    if (data.success) {
      setUploadStatus(`Sent "${data.asset.displayName}" (${(data.asset.fileSizeBytes / 1024).toFixed(0)} KB) to Headset!`);
    } else {
      setUploadStatus(`Error: ${data.error}`);
    }
  };

  return (
    <div style={{ display: 'flex', height: '100vh', background: '#0b0f17', color: '#f0f4f8', fontFamily: 'sans-serif' }}>
      <div style={{ width: '360px', padding: '20px', borderRight: '1px solid #223044' }}>
        <h2>Island Companion Portal</h2>
        <p style={{ fontSize: '13px', color: '#88a0b8' }}>
          Normalize 3D scale against the 1.8m green human cage before KTX2 + Draco cloud transcoding.
        </p>

        <div style={{ marginBottom: '16px' }}>
          <label>1. Headset 6-Digit Pairing Code (10m TTL)</label>
          <div style={{ display: 'flex', gap: '8px', marginTop: '6px' }}>
            <input
              value={pairingCode}
              onChange={(e) => setPairingCode(e.target.value)}
              placeholder="e.g. 482910"
              style={{ flex: 1, padding: '8px', background: '#162030', color: '#fff', border: '1px solid #334860' }}
            />
            <button onClick={verifyPairingCode} style={{ padding: '8px 14px', background: '#00c46a', color: '#000', fontWeight: 'bold' }}>
              Pair
            </button>
          </div>
        </div>

        <div style={{ marginBottom: '16px' }}>
          <label>2. Upload 3D Model (.glb, .fbx, .obj, .blend, .stl, .dae)</label>
          <input type="file" onChange={handleFileSelect} style={{ marginTop: '6px', width: '100%' }} />
        </div>

        <div style={{ marginBottom: '16px' }}>
          <label>3. Unit Scale Preset (Fixes glTFast Unit Scale)</label>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '6px', marginTop: '6px' }}>
            <button onClick={() => setUnitScale(0.001)}>mm → m (0.001x)</button>
            <button onClick={() => setUnitScale(0.01)}>cm → m (0.01x)</button>
            <button onClick={() => setUnitScale(0.0254)}>inch → m (0.0254x)</button>
            <button onClick={() => setUnitScale(1.0)}>Meters (1.0x)</button>
          </div>
        </div>

        <div style={{ marginBottom: '16px' }}>
          <label>
            <input type="checkbox" checked={centerBottomPivot} onChange={(e) => setCenterBottomPivot(e.target.checked)} />
            {' '}Snap Pivot to Bottom-Center (Ground Placement)
          </label>
        </div>

        <div style={{ marginBottom: '16px' }}>
          <label>4. KTX2 BasisU Texture Cap (Avoids 4K Crash & WebP)</label>
          <select
            value={maxTexSize}
            onChange={(e) => setMaxTexSize(e.target.value)}
            style={{ width: '100%', padding: '8px', marginTop: '6px', background: '#162030', color: '#fff' }}
          >
            <option value="1024">1024 × 1024 px (Standard Prop - Recommended)</option>
            <option value="2048">2048 × 2048 px (Hero Building Module)</option>
          </select>
        </div>

        <div style={{ padding: '10px', background: '#162030', borderRadius: '6px', marginBottom: '16px', fontSize: '13px' }}>
          <div><strong>Source Triangles:</strong> {modelStats.triangles.toLocaleString()}</div>
          <div><strong>Real-World Size (W×H×D):</strong> {modelStats.dimensionsMeters.join('m × ')}m</div>
          <div><strong>Target Gate:</strong> &lt; 15 MB KTX2 + Draco GLB</div>
        </div>

        <button
          disabled={!isPaired || !selectedFile}
          onClick={handleSanitizeAndSendToQuest}
          style={{ width: '100%', padding: '12px', background: isPaired ? '#00ff88' : '#445566', color: '#000', fontWeight: 'bold' }}
        >
          Sanitize & Push to Meta Quest
        </button>

        {uploadStatus && <p style={{ marginTop: '12px', color: '#00ff88', fontSize: '13px' }}>{uploadStatus}</p>}
      </div>

      <div ref={mountRef} style={{ flex: 1, height: '100%' }} />
    </div>
  );
}
