#!/bin/bash -x

echo "This script will setup a repository with a single changelog without previous releases."

source helpers.sh

setup_repo

touch CHANGELOG.md

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
