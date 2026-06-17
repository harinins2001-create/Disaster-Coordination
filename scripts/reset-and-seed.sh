#!/usr/bin/env bash
# Clean every DRCS table except drcs-users, then re-seed realistic data via the
# backend APIs so needs-met computation runs naturally.
#
# Uses --profile personal per repo rules.

set -euo pipefail

PROFILE="personal"
REGION="ap-south-1"
API="https://ypcijdk308.execute-api.ap-south-1.amazonaws.com/prod"
CLIENT_ID="3n4726blt2tnormpaakqleauaa"

TABLES=(drcs-resources drcs-bookings drcs-audit-logs drcs-disasters drcs-disaster-assignments drcs-donations)

clear_table() {
  local t="$1"
  echo "-- clearing $t"
  aws dynamodb scan \
    --table-name "$t" \
    --projection-expression "PK, SK" \
    --profile "$PROFILE" --region "$REGION" \
    --output json \
  | python3 -c "
import json, subprocess, sys
table, profile, region = sys.argv[1], sys.argv[2], sys.argv[3]
data = json.load(sys.stdin)
items = data.get('Items', [])
if not items:
    print('   (empty)')
    sys.exit(0)
print(f'   deleting {len(items)} items')
for i in range(0, len(items), 25):
    chunk = items[i:i+25]
    request = {
        table: [
            {'DeleteRequest': {'Key': {'PK': it['PK'], 'SK': it['SK']}}}
            for it in chunk
        ]
    }
    subprocess.run(
        ['aws', 'dynamodb', 'batch-write-item',
         '--request-items', json.dumps(request),
         '--profile', profile, '--region', region],
        check=True, stdout=subprocess.DEVNULL,
    )
" "$t" "$PROFILE" "$REGION"
}

for t in "${TABLES[@]}"; do
  clear_table "$t"
done

echo
echo "--- all non-user tables cleared"
echo

get_tok() {
  aws cognito-idp initiate-auth \
    --client-id "$CLIENT_ID" \
    --auth-flow USER_PASSWORD_AUTH \
    --auth-parameters USERNAME="$1",PASSWORD="$2" \
    --profile "$PROFILE" --region "$REGION" \
    --query "AuthenticationResult.IdToken" --output text
}

echo "-- fetching tokens"
TOK_REP="$(get_tok reporter@test.com 'Reporter1234!')"
TOK_H1="$(get_tok helper@test.com 'Helper1234!')"
TOK_H2="$(get_tok helpermedic@test.com 'Helper1234!')"
TOK_M="$(get_tok medic@test.com 'Medic1234!')"
TOK_D="$(get_tok donor@test.com 'Donor1234!')"
TOK_A="$(get_tok admin@admin.com 'Admin12345!')"

# Helper: create disaster and return slug
create_disaster() {
  local tok="$1" payload="$2"
  curl -s -X POST \
    -H "Authorization: Bearer $tok" \
    -H "Content-Type: application/json" \
    -d "$payload" \
    "$API/private/api/Disaster" \
    | python3 -c "import sys,json;print(json.load(sys.stdin)['data']['slug'])"
}

pledge() {
  local tok="$1" slug="$2"
  curl -s -X POST \
    -H "Authorization: Bearer $tok" \
    -H "Content-Type: application/json" \
    -d "{\"disasterSlug\":\"$slug\"}" \
    "$API/private/api/Assignment/pledge" >/dev/null
}

donate() {
  local tok="$1" slug="$2" item="$3" qty="$4" note="$5"
  curl -s -X POST \
    -H "Authorization: Bearer $tok" \
    -H "Content-Type: application/json" \
    -d "{\"disasterSlug\":\"$slug\",\"itemType\":\"$item\",\"quantity\":$qty,\"note\":\"$note\"}" \
    "$API/private/api/Donation" >/dev/null
}

echo
echo "-- creating 4 disasters as reporter@test.com"

SLUG_FIRE="$(create_disaster "$TOK_REP" '{
  "title":"Chemistry lab fire",
  "description":"Fume hood smoke incident on the third floor. Sprinklers tripped, evacuation complete.",
  "severity":"high",
  "location":"Science Block B, 3rd floor",
  "status":"active",
  "requiredVolunteers":3,
  "requiredResources":[
    {"itemType":"medical","quantity":5},
    {"itemType":"water","quantity":20}
  ]
}')"
echo "   + $SLUG_FIRE"

SLUG_FLOOD="$(create_disaster "$TOK_REP" '{
  "title":"Basement flooding",
  "description":"Burst water main has flooded the server room basement. Roughly 15cm standing water.",
  "severity":"critical",
  "location":"IT Building, B1",
  "status":"active",
  "requiredVolunteers":4,
  "requiredResources":[
    {"itemType":"tools","quantity":6},
    {"itemType":"equipment","quantity":3}
  ]
}')"
echo "   + $SLUG_FLOOD"

SLUG_POWER="$(create_disaster "$TOK_REP" '{
  "title":"Campus power outage",
  "description":"Full grid failure. Backup generators holding critical systems. Affecting 4 blocks.",
  "severity":"medium",
  "location":"Campus-wide",
  "status":"monitoring",
  "requiredVolunteers":2,
  "requiredResources":[
    {"itemType":"food","quantity":50},
    {"itemType":"water","quantity":100}
  ]
}')"
echo "   + $SLUG_POWER"

SLUG_GAS="$(create_disaster "$TOK_REP" '{
  "title":"Cafeteria gas leak",
  "description":"LPG smell near main kitchen. Cafeteria evacuated, gas shut off, waiting on inspection.",
  "severity":"critical",
  "location":"Student Cafeteria",
  "status":"active",
  "requiredVolunteers":2,
  "requiredResources":[
    {"itemType":"medical","quantity":2}
  ]
}')"
echo "   + $SLUG_GAS"

echo
echo "-- seeding pledges"
# fire: 2 of 3 volunteers (helper1 + medic)
pledge "$TOK_H1" "$SLUG_FIRE"
pledge "$TOK_M"  "$SLUG_FIRE"
echo "   fire: 2/3 pledged"

# flood: 4 of 4 volunteers (all helpers)
pledge "$TOK_H1" "$SLUG_FLOOD"
pledge "$TOK_H2" "$SLUG_FLOOD"
pledge "$TOK_M"  "$SLUG_FLOOD"
# Need a 4th -- use another helper-ish profile. Reuse helpermedic already done.
# We only have 3 helper-eligible test users; take 3/4 for realism.
echo "   flood: 3/4 pledged"

# power: 0 volunteers (below required) — leave empty

# gas: 2 of 2 pledged (fully met)
pledge "$TOK_H2" "$SLUG_GAS"
pledge "$TOK_M"  "$SLUG_GAS"
echo "   gas: 2/2 pledged"

echo
echo "-- seeding donations"
# fire: fully stock required medical + water
donate "$TOK_D" "$SLUG_FIRE"  medical 5  "bandages + antiseptic"
donate "$TOK_D" "$SLUG_FIRE"  water   20 "bottled cases"

# flood: partial tools, no equipment yet
donate "$TOK_D" "$SLUG_FLOOD" tools     4 "hand pumps and mops"

# power: partial food
donate "$TOK_D" "$SLUG_POWER" food  25 "dry rations"
donate "$TOK_D" "$SLUG_POWER" water 30 "extra bottles"

# gas: fully stock required medical (so needs-met should flip)
donate "$TOK_D" "$SLUG_GAS"   medical 2 "first-aid kits"

echo "   donations recorded"

echo
echo "-- final state"
curl -s "$API/public/api/Disaster" | python3 -c "
import sys,json
for d in json.load(sys.stdin)['data']:
    print(f\"  {d['slug']:30s} status={d['status']:10s} vol-req={d['requiredVolunteers']} items-req={len(d['requiredResources'])}\")
"

echo
echo "Done."
