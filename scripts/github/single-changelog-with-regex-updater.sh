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
    - replace_regex:
        file: src/Preamble.fs
        pattern: (?<=let version = ")(.*?)(?=")
---

This is the changelog for project A.
EOF

mkdir -p src

cat > src/Preamble.fs <<EOL
module Preamble

[<Literal>]
let version = "0.0.0"

let description = "This is a sample F# project."
EOL

git add .
git commit -m "chore: initial commit"
git push


echo "Feature 1" >> src/feature1.txt
git add .
git commit -m "feat: add feature 1"

echo "Feature 2" >> src/feature2.txt
git add .
git commit -m "feat: add feature 2"


# Generate changelog
dotnet run --project ../../../src/
