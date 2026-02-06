#!/bin/bash -x

echo "
This script will setup a repository with a multiple changelogs and a shared library:

This is to test that the changelog generation is triggered even when
the changes are inside of the shared library.

This script will also run the changelog generation several times,
as if we were adding multiple features and re-generating the changelog after each one.
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
include:
    - ../SharedLibrary/
---

This is the changelog for project A.
EOF

git add .
git commit -m "chore: setup project A with shared library"

# Generate changelog
dotnet run --project ../../../src/ -- github

# Setup Project B which depends on SharedLibrary too
cat > src/ProjectB/CHANGELOG.md<< EOF
---
name: Project B
include:
    - ../SharedLibrary/
---
This is the changelog for project B.
EOF

git add .
git commit -m "chore: setup project B with shared library"

# Generate changelog again after adding Project B
dotnet run --project ../../../src/ -- github
# Give some time for the changes to propagate
sleep 2

# Now add feature to project A only
echo "Feature A.1" >> src/ProjectA/featureA1.txt
git add .
git commit -m "feat: add feature A.1"

# Add a feature to the shared library
echo "Shared library new feature" >> src/SharedLibrary/feature2.txt
git add .
git commit -m "feat: add new feature to shared library"

# Generate changelog again after adding features
dotnet run --project ../../../src/ -- github
