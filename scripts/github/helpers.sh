REPO_DIR="temp"

setup_repo () {
    # Delete existing GitHub repository if it exists
    gh repo delete this-is-a-test --yes 2>/dev/null || true

    # Clean up any existing repository
    rm -rf "$REPO_DIR"
    mkdir -p "$REPO_DIR"
    cd "$REPO_DIR" || exit 1
    git init -b main

    # Create a new GitHub repository using the GitHub CLI
    gh repo create this-is-a-test --private --source=.

    touch README.md

    git add .
    git commit -m "chore: initial commit"
    git push
}
