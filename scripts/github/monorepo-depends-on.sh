#!/bin/bash -x

echo "
This script will setup a repository with multiple changelogs linked by depends_on:

- Core
- Plugin
- Dom depends on Core and Plugin
- Form depends on Dom
- Docs has no dependencies

Expected release:

- Plugin is released because of a fix
- Dom is released because Plugin is released
- Form is released because of its own feature and because Dom is released
- Core is not released, a chore commit does not trigger its dependents
- Docs is not released
"

source helpers.sh

setup_repo

mkdir -p src/Core
mkdir -p src/Plugin
mkdir -p src/Dom
mkdir -p src/Form
mkdir -p src/Docs

cat > src/Core/CHANGELOG.md<< EOF
---
name: Core
---

This is the changelog for Core.
EOF

cat > src/Plugin/CHANGELOG.md<< EOF
---
name: Plugin
---

This is the changelog for Plugin.
EOF

cat > src/Dom/CHANGELOG.md<< EOF
---
name: Dom
depends_on:
    - ../Core/
    - ../Plugin/
---

This is the changelog for Dom.
EOF

cat > src/Form/CHANGELOG.md<< EOF
---
name: Form
depends_on:
    - ../Dom/
---

This is the changelog for Form.
EOF

cat > src/Docs/CHANGELOG.md<< EOF
---
name: Docs
---

This is the changelog for Docs.
EOF

git add .
git commit -m "chore: setup projects"

echo "Core code" >> src/Core/core.txt
git add .
git commit -m "chore: tidy Core"

echo "Plugin code" >> src/Plugin/plugin.txt
git add .
git commit -m "fix: fix a bug in Plugin"

echo "Form code" >> src/Form/form.txt
git add .
git commit -m "feat: add a feature to Form"

# Generate changelog
dotnet run --project ../../../src/ -- github
