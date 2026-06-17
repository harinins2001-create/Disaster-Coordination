#!/usr/bin/env bash
# Seed DRCS users in Cognito + DynamoDB.
# Non-negotiable: uses --profile personal for every AWS call.
#
# Idempotent: if a user already exists in Cognito we skip creation, re-set the
# password, and upsert the DynamoDB row.

set -euo pipefail

PROFILE="${AWS_PROFILE_OVERRIDE:-personal}"
REGION="ap-south-1"
USERS_TABLE="drcs-users"

get_output() {
  aws cloudformation describe-stacks \
    --stack-name DrcsStack \
    --profile "$PROFILE" --region "$REGION" \
    --query "Stacks[0].Outputs[?OutputKey=='$1'].OutputValue" \
    --output text
}

USER_POOL_ID="$(get_output UserPoolId)"
if [[ -z "$USER_POOL_ID" ]]; then
  echo "Could not resolve UserPoolId from DrcsStack" >&2
  exit 1
fi
echo "Using user pool: $USER_POOL_ID"

iso_now() { date -u +"%Y-%m-%dT%H:%M:%S.%3NZ" 2>/dev/null || date -u +"%Y-%m-%dT%H:%M:%SZ"; }

# Usage: seed_user <email> <password> <name> <nic> <phone> <dob> <gender> <area> <roles-csv>
seed_user() {
  local email="$1" password="$2" name="$3" nic="$4" phone="$5" dob="$6" gender="$7" area="$8" roles_csv="$9"

  echo "-- Seeding $email ($roles_csv)"

  local sub
  sub="$(aws cognito-idp admin-get-user \
        --user-pool-id "$USER_POOL_ID" \
        --username "$email" \
        --profile "$PROFILE" --region "$REGION" \
        --query "UserAttributes[?Name=='sub'].Value | [0]" \
        --output text 2>/dev/null || true)"

  if [[ -z "$sub" || "$sub" == "None" ]]; then
    sub="$(aws cognito-idp admin-create-user \
          --user-pool-id "$USER_POOL_ID" \
          --username "$email" \
          --message-action SUPPRESS \
          --user-attributes Name=email,Value="$email" Name=email_verified,Value=true Name=name,Value="$name" \
          --profile "$PROFILE" --region "$REGION" \
          --query "User.Attributes[?Name=='sub'].Value | [0]" \
          --output text)"
    echo "   created Cognito user, sub=$sub"
  else
    echo "   user exists, sub=$sub"
  fi

  aws cognito-idp admin-set-user-password \
    --user-pool-id "$USER_POOL_ID" \
    --username "$email" \
    --password "$password" \
    --permanent \
    --profile "$PROFILE" --region "$REGION" >/dev/null

  local pk="USER#$sub"
  local now
  now="$(iso_now)"

  # Build roles list JSON from CSV
  local roles_json
  roles_json="$(python3 -c "import json,sys;print(json.dumps(sys.argv[1].split(',')))" "$roles_csv")"

  local item
  item="$(python3 - "$pk" "$sub" "$email" "$name" "$nic" "$phone" "$dob" "$gender" "$area" "$roles_json" "$now" <<'PY'
import json, sys
pk, sub, email, name, nic, phone, dob, gender, area, roles_json, now = sys.argv[1:]
roles = json.loads(roles_json)
item = {
    "PK":            {"S": pk},
    "SK":            {"S": pk},
    "entity":        {"S": "user"},
    "sub":           {"S": sub},
    "email":         {"S": email},
    "name":          {"S": name},
    "nic":           {"S": nic},
    "phone":         {"S": phone},
    "dob":           {"S": dob},
    "gender":        {"S": gender},
    "photoKey":      {"S": ""},
    "area":          {"S": area},
    "skills":        {"L": []},
    "travelMethods": {"L": []},
    "roles":         {"L": [{"S": r} for r in roles]},
    "active":        {"BOOL": True},
    "createdAt":     {"S": now},
}
print(json.dumps(item))
PY
)"

  aws dynamodb put-item \
    --table-name "$USERS_TABLE" \
    --item "$item" \
    --profile "$PROFILE" --region "$REGION" >/dev/null

  echo "   upserted $USERS_TABLE row"
}

# Admin (single seeded super-user)
seed_user admin@admin.com    'Admin12345!'    'DRCS Admin'         200012300123 '+94770000001' '2000-01-01' M Colombo  'admin'

# Test users per role (for smoke testing)
seed_user moderator@test.com 'Moderator1234!' 'Test Moderator'      199212345678 '+94770000007' '1992-04-04' F Colombo  'moderator'
seed_user helper@test.com    'Helper1234!'    'Test Helper'         199812345678 '+94770000003' '1998-07-07' F Kandy    'helper'
seed_user medic@test.com     'Medic1234!'     'Test Medic'          198512345678 '+94770000004' '1985-02-02' M Galle    'medic'
seed_user helpermedic@test.com 'Helper1234!'  'Cross-role Helper'   198712345678 '+94770000006' '1987-03-03' M Jaffna   'helper,medic'

echo "Done."
