#!/bin/bash
# Usage: ./scripts/release.sh 1.2.0
set -e

VERSION=$1

if [ -z "$VERSION" ]; then
  echo "Error: version argument required"
  echo "Usage: ./scripts/release.sh 1.2.0"
  exit 1
fi

echo "Releasing v$VERSION..."

# 1. Bump package.json version (without creating a git tag)
npm --prefix predictions-ui version "$VERSION" --no-git-tag-version

# 2. Commit the version bump
git add predictions-ui/package.json
git commit -m "chore: release v$VERSION"

# 3. Tag and push
git tag "v$VERSION"
git push origin master
git push origin "v$VERSION"

echo "Done. Tag v$VERSION pushed — release workflow will now run tests and deploy."
