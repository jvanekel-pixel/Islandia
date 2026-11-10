# Islandia — the commands this project is driven by.
#
# Everything here runs in a cloud session with no Unity and no headset. Unity
# work happens in GitHub Actions (docs/plan/04-VERIFICATION.md).
#
# Run `make` or `make help` for the list.

SHELL := /bin/bash
PY    := python3

.DEFAULT_GOAL := help

# ---------------------------------------------------------------- housekeeping

.PHONY: help
help:  ## Show this list
	@echo "Islandia — make targets"
	@echo
	@grep -hE '^[a-zA-Z0-9_-]+:.*?## ' $(MAKEFILE_LIST) \
	  | sort \
	  | awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[1m%-16s\033[0m %s\n", $$1, $$2}'
	@echo
	@echo "  Plan: docs/plan/00-START-HERE.md (owner) · CLAUDE.md (sessions)"

.PHONY: deps
deps:  ## Install the Python packages the tooling needs
	@$(PY) -m pip install --quiet --disable-pip-version-check -r tools/requirements.txt
	@echo "deps: ready"

# ----------------------------------------------------------------------- gates

.PHONY: check
check: lint tools-test core-test companion-test  ## Everything provable without Unity. Run before every push.
	@echo
	@echo "check: all local gates passed"

.PHONY: lint
lint: lint-plan lint-privacy lint-markdown lint-meta lint-layering  ## Static checks (verification level L0)

.PHONY: lint-plan
lint-plan:  ## The backlog and plan are internally consistent
	@$(PY) tools/lint/check_plan.py

.PHONY: lint-privacy
lint-privacy:  ## Nothing private is committed to this public repository
	@$(PY) tools/lint/check_privacy.py

.PHONY: lint-markdown
lint-markdown:  ## Internal links and file references in the docs resolve
	@$(PY) tools/lint/check_markdown.py

.PHONY: lint-meta
lint-meta:  ## Every generated Unity .meta is present and current
	@$(PY) tools/gen_meta.py --check


.PHONY: lint-layering
lint-layering:  ## Island.Core stays engine-free, so the fast tests keep working
	@$(PY) tools/lint/check_layering.py

# ------------------------------------------------------------------- the tests

.PHONY: tools-test
tools-test:  ## Tests for this repository's own tooling
	@$(PY) -m unittest discover -s tools/tests -t tools/tests

# A cloud session has no .NET SDK; F-10's setup script will install one, and until then a
# session installs it under ~/.dotnet. Look there as well as on PATH.
DOTNET := $(shell command -v dotnet 2>/dev/null || echo $(HOME)/.dotnet/dotnet)

.PHONY: core-test
core-test:  ## Island.Core tests — engine-free C#, about a second (level L1)
	@if [ ! -x "$(DOTNET)" ]; then \
	  echo "core-test: no .NET SDK found. Install one with:"; \
	  echo "  curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 9.0 --install-dir \$$HOME/.dotnet"; \
	  exit 1; \
	fi
	@"$(DOTNET)" test dotnet/Island.Core.Tests --nologo --verbosity quiet

.PHONY: core-build
core-build:  ## Compile Island.Core alone, the way Unity will (netstandard2.1)
	@"$(DOTNET)" build dotnet/Island.Core --nologo --verbosity quiet

.PHONY: companion-test
companion-test:  ## Web converter and relay tests (level L1/L3)
	@if [ -f companion/package.json ]; then \
	  npm --prefix companion test; \
	else \
	  echo "companion-test: the companion does not exist yet (work packages I-04, I-05)"; \
	fi

# ------------------------------------------------------------------- utilities

.PHONY: meta
meta:  ## Write any missing or stale Unity .meta files
	@$(PY) tools/gen_meta.py

.PHONY: plan
plan:  ## Where the project stands right now
	@sed -n '1,40p' docs/plan/STATE.md

.PHONY: next
next:  ## The next unblocked work packages
	@$(PY) -c "import yaml, sys; \
	d = yaml.safe_load(open('docs/plan/BACKLOG.yaml')); \
	p = d['packages']; \
	done = {x['id'] for x in p if x['status'] == 'done'}; \
	ready = [x for x in p if x['status'] == 'todo' and all(dep in done for dep in x['deps'])]; \
	print('next unblocked work packages:') or [print(f\"  {x['id']:<14} {x['ms']:<4} {x['title']}\") for x in ready[:8]] if ready else print('  none — every remaining package is blocked or needs a human')"

# ------------------------------------------------------- runs in CI, not here

.PHONY: unity-test
unity-test:  ## (CI only) EditMode and PlayMode tests — levels L2 and L3
	@echo "unity-test: runs in GitHub Actions; there is no Unity in a cloud session."
	@echo "            See docs/plan/04-VERIFICATION.md and the unity-tests workflow."

.PHONY: build
build:  ## (CI only) IL2CPP ARM64 Android build — level L4
	@echo "build: runs in GitHub Actions on merge to main, or on demand."
	@echo "       See docs/plan/04-VERIFICATION.md and the android-build workflow."
