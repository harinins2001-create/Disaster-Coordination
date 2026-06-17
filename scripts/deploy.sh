#!/usr/bin/env bash
# One-shot deploy: backend infra (CDK) + frontend push to CodeCommit.
#
# Flow:
#   1. cdk deploy              — builds lambdas, updates API/DDB/Cognito,
#                                 creates/updates CodeCommit repo + Amplify app
#   2. git remote add codecommit (first time only) from the stack output
#   3. git push codecommit     — Amplify auto-builds on the new commit
#
# Run from repo root. Requires: dotnet, cdk, aws, git.

set -euo pipefail

AWS_PROFILE_NAME=personal
STACK_NAME=DrcsStack
REGION=ap-south-1

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

echo "==> [1/4] CDK build + deploy"
(
  cd backend
  dotnet build src/Infrastructure/Infrastructure.csproj -v q
  cdk deploy --profile "$AWS_PROFILE_NAME" --require-approval never "$@"
)

echo "==> [2/4] Reading stack outputs"
REPO_URL=$(aws cloudformation describe-stacks \
  --stack-name "$STACK_NAME" \
  --profile "$AWS_PROFILE_NAME" \
  --region "$REGION" \
  --query "Stacks[0].Outputs[?OutputKey=='CodeCommitCloneUrl'].OutputValue" \
  --output text)

AMPLIFY_URL=$(aws cloudformation describe-stacks \
  --stack-name "$STACK_NAME" \
  --profile "$AWS_PROFILE_NAME" \
  --region "$REGION" \
  --query "Stacks[0].Outputs[?OutputKey=='AmplifyUrl'].OutputValue" \
  --output text)

if [ -z "$REPO_URL" ] || [ "$REPO_URL" = "None" ]; then
  echo "error: CodeCommitCloneUrl not found in stack outputs. Aborting push."
  exit 1
fi

echo "    CodeCommit: $REPO_URL"
echo "    Amplify:    $AMPLIFY_URL"

echo "==> [3/4] Configuring git remote + credential helper"
if ! git remote get-url codecommit >/dev/null 2>&1; then
  git remote add codecommit "$REPO_URL"
  echo "    added remote 'codecommit'"
else
  # Keep remote URL in sync in case it ever changes
  git remote set-url codecommit "$REPO_URL"
fi

# Tell git to use the AWS credential helper for this repo (scoped to --local)
git config --local credential.helper "!aws codecommit credential-helper \$@ --profile $AWS_PROFILE_NAME"
git config --local credential.UseHttpPath true

echo "==> [4/4] Pushing to CodeCommit (triggers Amplify build)"
CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD)
git push codecommit "$CURRENT_BRANCH:main"

echo ""
echo "done."
echo "  Amplify build:   https://console.aws.amazon.com/amplify/home?region=$REGION"
echo "  Frontend URL:    $AMPLIFY_URL"
echo "  (first build can take 5-10 min)"
