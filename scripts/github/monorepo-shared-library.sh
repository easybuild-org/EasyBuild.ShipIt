#!/bin/bash -x

echo "
This script will setup a repository with a multiple changelogs and a shared library:

This is to test that the changelog generation is triggered even when
the changes are inside of the shared library.
"

source helpers.sh

setup_repo

mkdir -p src/SharedLibrary
mkdir -p src/ProjectA
mkdir -p src/ProjectB

# Add commit for shared library
echo "Shared library code" >> src/SharedLibrary/feature.txt
git add .
git commit -m "feat: add shared library code"

# Setup Project A which depends on SharedLibrary
cat > src/ProjectA/CHANGELOG.md<< EOF
---
name: Project A
include:
    - ../SharedLibrary/
---

This is the changelog for project A.
EOF

git add .
git commit -m "chore: setup project A with shared library"

# Generate changelog
dotnet run --project ../../../src/ -- github
