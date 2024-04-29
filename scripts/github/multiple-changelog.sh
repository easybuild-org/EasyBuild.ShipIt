#!/bin/bash -x

echo "
This script will setup a repository with a multiple changelogs:

Only one of the changelog project-a will need a release.
"

source helpers.sh

setup_repo

# Scaffold multiple projects
mkdir -p src/project-a
mkdir -p src/project-b

echo "Feature A.1" >> src/project-a/featureA1.txt
echo "Feature A.2" >> src/project-a/featureA2.txt
touch src/project-a/CHANGELOG.md

git add .
git commit -m "feat: add features to project A"

# Add commit for project B but they will not need a release
touch src/project-b/deps.txt
cat > src/project-b/CHANGELOG.md<< EOF
---
name: Project B
---

This is the changelog for project B.
EOF
git add .
git commit -m "chore: setup features in project B"


# Generate changelog
dotnet run --project ../../../src/
