#!/bin/bash -x

echo """
This script will setup a repository with a single changelog without previous releases.

It will also setup a package.json file to demonstrate the package.json updater.
"""

source helpers.sh

setup_repo

cat > CHANGELOG.md<< EOF
---
name: Project A
updaters:
    - package.json:
        file: package.json
---

This is the changelog for project A.
EOF

cat > package.json <<EOL
{
   "name": "sample-package",
    "version": "1.0.0"
}
EOL

git add .
git commit -m "chore: initial commit"
git push

mkdir -p src

echo "Feature 1" >> src/feature1.txt
git add .
git commit -m "feat: add feature 1"

echo "Feature 2" >> src/feature2.txt
git add .
git commit -m "feat: add feature 2"


# Generate changelog
dotnet run --project ../../../src/
