#!/usr/bin/env bash
# Seed DRCS realistic dataset: 7 citizens + 9 disasters + assignments/donations/resources.
# Dates span 2026-03-15 → 2026-04-18 to simulate ~1 month of operation.
# Idempotent: re-running overwrites rows (put-item / admin-set-user-password).
# Requires seed-users.sh to have been run first (admin/moderator/helper/medic/helpermedic).

set -euo pipefail

PROFILE="${AWS_PROFILE_OVERRIDE:-personal}"
REGION="ap-south-1"
USERS_TABLE="drcs-users"
DISASTERS_TABLE="drcs-disasters"
ASSIGNMENTS_TABLE="drcs-disaster-assignments"
DONATIONS_TABLE="drcs-donations"
RESOURCES_TABLE="drcs-resources"

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

# -----------------------------------------------------------------------------
# Lookup existing user subs (admin/moderator/helper/medic/helpermedic)
# -----------------------------------------------------------------------------
lookup_sub() {
  local email="$1"
  aws cognito-idp admin-get-user \
    --user-pool-id "$USER_POOL_ID" \
    --username "$email" \
    --profile "$PROFILE" --region "$REGION" \
    --query "UserAttributes[?Name=='sub'].Value | [0]" \
    --output text 2>/dev/null || echo ""
}

SUB_ADMIN="$(lookup_sub admin@admin.com)"
SUB_MOD="$(lookup_sub moderator@test.com)"
SUB_HELPER="$(lookup_sub helper@test.com)"
SUB_MEDIC="$(lookup_sub medic@test.com)"
SUB_HM="$(lookup_sub helpermedic@test.com)"

for pair in "admin:$SUB_ADMIN" "mod:$SUB_MOD" "helper:$SUB_HELPER" "medic:$SUB_MEDIC" "helpermedic:$SUB_HM"; do
  name="${pair%%:*}"; val="${pair##*:}"
  if [[ -z "$val" || "$val" == "None" ]]; then
    echo "Missing Cognito sub for $name — run seed-users.sh first" >&2
    exit 1
  fi
done

# -----------------------------------------------------------------------------
# Seed normal citizens (7 new users)
# -----------------------------------------------------------------------------
seed_user() {
  local email="$1" password="$2" name="$3" nic="$4" phone="$5" dob="$6" gender="$7" area="$8" roles_csv="$9" created_at="${10}"

  echo "-- Seeding citizen $email"

  local sub
  sub="$(lookup_sub "$email")"

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
  local roles_json
  roles_json="$(python3 -c "import json,sys;print(json.dumps(sys.argv[1].split(',')))" "$roles_csv")"

  local item
  item="$(python3 - "$pk" "$sub" "$email" "$name" "$nic" "$phone" "$dob" "$gender" "$area" "$roles_json" "$created_at" <<'PY'
import json, sys
pk, sub, email, name, nic, phone, dob, gender, area, roles_json, created_at = sys.argv[1:]
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
    "createdAt":     {"S": created_at},
}
print(json.dumps(item))
PY
)"

  aws dynamodb put-item \
    --table-name "$USERS_TABLE" \
    --item "$item" \
    --profile "$PROFILE" --region "$REGION" >/dev/null

  # expose as global var CURRENT_SUB for downstream use
  CURRENT_SUB="$sub"
}

# Seed 7 citizens with varied districts/roles. createdAt = 2026-03-01 (onboarded pre-disasters).
seed_user citizen1@test.com 'Citizen1234!' 'Sunil Perera'         198503120123 '+94771100001' '1985-03-12' M Colombo 'helper'        '2026-03-01T09:00:00.0000000Z'
SUB_C1="$CURRENT_SUB"
seed_user citizen2@test.com 'Citizen1234!' 'Nimal Silva'          199208220456 '+94771100002' '1992-08-22' M Kandy   'helper'        '2026-03-02T09:00:00.0000000Z'
SUB_C2="$CURRENT_SUB"
seed_user citizen3@test.com 'Citizen1234!' 'Kamala Jayawardena'   198811150789 '+94771100003' '1988-11-15' F Galle   'medic'         '2026-03-03T09:00:00.0000000Z'
SUB_C3="$CURRENT_SUB"
seed_user citizen4@test.com 'Citizen1234!' 'Ruwan Fernando'       199502280321 '+94771100004' '1995-02-28' M Jaffna  'helper'        '2026-03-04T09:00:00.0000000Z'
SUB_C4="$CURRENT_SUB"
seed_user citizen5@test.com 'Citizen1234!' 'Dilhani Bandara'      199007040654 '+94771100005' '1990-07-04' F Matara  'helper,medic'  '2026-03-05T09:00:00.0000000Z'
SUB_C5="$CURRENT_SUB"
seed_user citizen6@test.com 'Citizen1234!' 'Pradeep Kumara'       198612190987 '+94771100006' '1986-12-19' M Colombo 'medic'         '2026-03-06T09:00:00.0000000Z'
SUB_C6="$CURRENT_SUB"
seed_user citizen7@test.com 'Citizen1234!' 'Samanthi Wickremasinghe' 199309070135 '+94771100007' '1993-09-07' F Kandy 'helper'        '2026-03-07T09:00:00.0000000Z'
SUB_C7="$CURRENT_SUB"

echo
echo "Users ready. Citizen subs: C1=$SUB_C1 ... C7=$SUB_C7"
echo

# -----------------------------------------------------------------------------
# Helper: name lookup for assignment/donation rows
# -----------------------------------------------------------------------------
NAME_ADMIN="DRCS Admin"
NAME_MOD="Test Moderator"
NAME_HELPER="Test Helper"
NAME_MEDIC="Test Medic"
NAME_HM="Cross-role Helper"
NAME_C1="Sunil Perera"
NAME_C2="Nimal Silva"
NAME_C3="Kamala Jayawardena"
NAME_C4="Ruwan Fernando"
NAME_C5="Dilhani Bandara"
NAME_C6="Pradeep Kumara"
NAME_C7="Samanthi Wickremasinghe"

EMAIL_ADMIN="admin@admin.com"
EMAIL_MOD="moderator@test.com"
EMAIL_HELPER="helper@test.com"
EMAIL_MEDIC="medic@test.com"
EMAIL_HM="helpermedic@test.com"
EMAIL_C1="citizen1@test.com"
EMAIL_C2="citizen2@test.com"
EMAIL_C3="citizen3@test.com"
EMAIL_C4="citizen4@test.com"
EMAIL_C5="citizen5@test.com"
EMAIL_C6="citizen6@test.com"
EMAIL_C7="citizen7@test.com"

# -----------------------------------------------------------------------------
# Seed disasters
# -----------------------------------------------------------------------------
# Args: slug title description severity location status reportedByEmail reportedBySub reportedByName
#       requiredVolunteers requiredResourcesJson photoKeysJson rejectionReason createdAt updatedAt
seed_disaster() {
  local slug="$1" title="$2" desc="$3" severity="$4" location="$5" status="$6"
  local by_email="$7" by_sub="$8" by_name="$9"
  local req_vol="${10}" req_res_json="${11}" photo_keys_json="${12}" reject="${13}"
  local created="${14}" updated="${15}"

  echo "-- Disaster: $slug ($status)"

  local item
  item="$(python3 - "$slug" "$title" "$desc" "$severity" "$location" "$status" \
                   "$by_email" "$by_sub" "$by_name" \
                   "$req_vol" "$req_res_json" "$photo_keys_json" "$reject" \
                   "$created" "$updated" <<'PY'
import json, sys
(slug, title, desc, severity, location, status,
 by_email, by_sub, by_name,
 req_vol, req_res_json, photo_keys_json, reject,
 created, updated) = sys.argv[1:]
req_res = json.loads(req_res_json)
photo_keys = json.loads(photo_keys_json)
item = {
    "PK":                 {"S": f"DISASTER#{slug}"},
    "SK":                 {"S": f"DISASTER#{slug}"},
    "entity":             {"S": "disaster"},
    "slug":               {"S": slug},
    "title":              {"S": title},
    "description":        {"S": desc},
    "severity":           {"S": severity},
    "location":           {"S": location},
    "status":             {"S": status},
    "reportedBy":         {"S": by_email},
    "reportedBySub":      {"S": by_sub},
    "reportedByName":     {"S": by_name},
    "requiredVolunteers": {"N": str(req_vol)},
    "requiredResources":  {"L": [
        {"M": {"itemType": {"S": r["itemType"]}, "quantity": {"N": str(r["quantity"])}}}
        for r in req_res
    ]},
    "photoKeys":          {"L": [{"S": p} for p in photo_keys]},
    "rejectionReason":    {"S": reject},
    "createdAt":          {"S": created},
}
if updated:
    item["updatedAt"] = {"S": updated}
print(json.dumps(item))
PY
)"

  aws dynamodb put-item \
    --table-name "$DISASTERS_TABLE" \
    --item "$item" \
    --profile "$PROFILE" --region "$REGION" >/dev/null
}

# --- 1. Colombo flood — closed (oldest, fully resolved)
seed_disaster \
  'colombo-flood-march' \
  'March Monsoon Flooding in Colombo' \
  'Heavy rain caused flash flooding across low-lying wards in Colombo. Roads impassable; homes damaged in Kelaniya area.' \
  'high' \
  'Colombo' \
  'closed' \
  "$EMAIL_HELPER" "$SUB_HELPER" "$NAME_HELPER" \
  20 \
  '[{"itemType":"water","quantity":500},{"itemType":"shelter","quantity":100}]' \
  '[]' \
  '' \
  '2026-03-15T08:00:00.0000000Z' \
  '2026-03-28T18:00:00.0000000Z'

# --- 2. Kandy landslide — needs-met
seed_disaster \
  'kandy-landslide-march' \
  'Landslide in Kandy Hillside Village' \
  'Hillside collapse following sustained rain isolated a village of ~80 families. Medical teams required.' \
  'critical' \
  'Kandy' \
  'needs-met' \
  "$EMAIL_HELPER" "$SUB_HELPER" "$NAME_HELPER" \
  30 \
  '[{"itemType":"water","quantity":500},{"itemType":"medical","quantity":30},{"itemType":"shelter","quantity":150}]' \
  '[]' \
  '' \
  '2026-03-22T10:00:00.0000000Z' \
  '2026-04-10T12:00:00.0000000Z'

# --- 3. Galle storm damage — active
seed_disaster \
  'galle-storm-march' \
  'Cyclonic Storm Damage in Galle' \
  'Storm knocked out power lines and damaged fishing boats along the coast. Ongoing cleanup effort needed.' \
  'medium' \
  'Galle' \
  'active' \
  "$EMAIL_MEDIC" "$SUB_MEDIC" "$NAME_MEDIC" \
  15 \
  '[{"itemType":"water","quantity":300},{"itemType":"shelter","quantity":60}]' \
  '[]' \
  '' \
  '2026-03-29T14:00:00.0000000Z' \
  '2026-04-15T09:00:00.0000000Z'

# --- 4. Jaffna market fire — active
seed_disaster \
  'jaffna-market-fire' \
  'Jaffna Central Market Fire' \
  'Fire gutted 12 shops at the central market overnight. No casualties reported. Owners need immediate assistance.' \
  'high' \
  'Jaffna' \
  'active' \
  "$EMAIL_HM" "$SUB_HM" "$NAME_HM" \
  10 \
  '[{"itemType":"water","quantity":200},{"itemType":"equipment","quantity":15}]' \
  '[]' \
  '' \
  '2026-04-02T05:30:00.0000000Z' \
  '2026-04-16T10:00:00.0000000Z'

# --- 5. Matara flood — active
seed_disaster \
  'matara-flood-april' \
  'April Flash Flood in Matara' \
  'Sudden cloudburst flooded residential neighbourhoods. Three school buildings inundated.' \
  'high' \
  'Matara' \
  'active' \
  "$EMAIL_C5" "$SUB_C5" "$NAME_C5" \
  25 \
  '[{"itemType":"water","quantity":400},{"itemType":"shelter","quantity":120},{"itemType":"food","quantity":100}]' \
  '[]' \
  '' \
  '2026-04-05T22:00:00.0000000Z' \
  '2026-04-17T08:00:00.0000000Z'

# --- 6. Colombo tree fall — active (smaller scale)
seed_disaster \
  'colombo-tree-fall' \
  'Large Tree Fall on Galle Road' \
  'Storm-weakened banyan tree fell across Galle Road blocking traffic and crushing two parked vehicles.' \
  'medium' \
  'Colombo' \
  'active' \
  "$EMAIL_C1" "$SUB_C1" "$NAME_C1" \
  8 \
  '[{"itemType":"tools","quantity":5},{"itemType":"equipment","quantity":50}]' \
  '[]' \
  '' \
  '2026-04-08T16:45:00.0000000Z' \
  '2026-04-15T11:00:00.0000000Z'

# --- 7. Galle bridge damage — rejected
seed_disaster \
  'galle-bridge-rejected' \
  'Suspected Bridge Damage near Galle' \
  'Reporter claims bridge supports are cracked. Photos attached.' \
  'medium' \
  'Galle' \
  'rejected' \
  "$EMAIL_C2" "$SUB_C2" "$NAME_C2" \
  0 \
  '[]' \
  '[]' \
  'Unable to verify any structural damage from the submitted photos. Please resubmit with clearer images showing the alleged cracks, or contact the Roads Development Authority directly.' \
  '2026-04-15T19:00:00.0000000Z' \
  '2026-04-16T09:30:00.0000000Z'

# --- 8. Colombo power outage — pending
seed_disaster \
  'colombo-power-outage' \
  'Prolonged Power Outage in Wellawatte' \
  'Power has been out for 14 hours across multiple blocks in Wellawatte. Elderly residents reporting health concerns.' \
  'medium' \
  'Colombo' \
  'pending' \
  "$EMAIL_C6" "$SUB_C6" "$NAME_C6" \
  5 \
  '[]' \
  '[]' \
  '' \
  '2026-04-17T20:00:00.0000000Z' \
  ''

# --- 9. Kandy water supply — pending
seed_disaster \
  'kandy-water-disruption' \
  'Water Supply Disruption in Kandy Town' \
  'Mains water has been out for over a day in central Kandy. Bowser distribution requested.' \
  'low' \
  'Kandy' \
  'pending' \
  "$EMAIL_C7" "$SUB_C7" "$NAME_C7" \
  0 \
  '[{"itemType":"water","quantity":300}]' \
  '[]' \
  '' \
  '2026-04-17T11:00:00.0000000Z' \
  ''

# -----------------------------------------------------------------------------
# Seed assignments (volunteer pledges)
# -----------------------------------------------------------------------------
# Args: disasterSlug userSub userName userEmail status createdAt updatedAt
seed_assignment() {
  local slug="$1" sub="$2" uname="$3" email="$4" status="$5" created="$6" updated="$7"

  local item
  item="$(python3 - "$slug" "$sub" "$uname" "$email" "$status" "$created" "$updated" <<'PY'
import json, sys
slug, sub, uname, email, status, created, updated = sys.argv[1:]
item = {
    "PK":            {"S": f"DISASTER#{slug}"},
    "SK":            {"S": f"VOL#{sub}"},
    "entity":        {"S": "assignment"},
    "disasterSlug":  {"S": slug},
    "userSub":       {"S": sub},
    "userName":      {"S": uname},
    "userEmail":     {"S": email},
    "userPhotoKey":  {"S": ""},
    "status":        {"S": status},
    "createdAt":     {"S": created},
}
if updated:
    item["updatedAt"] = {"S": updated}
print(json.dumps(item))
PY
)"

  aws dynamodb put-item \
    --table-name "$ASSIGNMENTS_TABLE" \
    --item "$item" \
    --profile "$PROFILE" --region "$REGION" >/dev/null

  echo "   assignment $slug / $uname ($status)"
}

# Disaster 1 — Colombo flood (closed): 3 pledges, all done
seed_assignment 'colombo-flood-march' "$SUB_HELPER" "$NAME_HELPER" "$EMAIL_HELPER" 'done' '2026-03-15T12:00:00.0000000Z' '2026-03-27T17:00:00.0000000Z'
seed_assignment 'colombo-flood-march' "$SUB_C1"     "$NAME_C1"     "$EMAIL_C1"     'done' '2026-03-16T09:00:00.0000000Z' '2026-03-27T17:00:00.0000000Z'
seed_assignment 'colombo-flood-march' "$SUB_C6"     "$NAME_C6"     "$EMAIL_C6"     'done' '2026-03-17T08:00:00.0000000Z' '2026-03-28T12:00:00.0000000Z'

# Disaster 2 — Kandy landslide (needs-met): 4 pledges (mix done + active)
seed_assignment 'kandy-landslide-march' "$SUB_HELPER" "$NAME_HELPER" "$EMAIL_HELPER" 'done'   '2026-03-22T11:00:00.0000000Z' '2026-04-08T16:00:00.0000000Z'
seed_assignment 'kandy-landslide-march' "$SUB_MEDIC"  "$NAME_MEDIC"  "$EMAIL_MEDIC"  'done'   '2026-03-22T14:00:00.0000000Z' '2026-04-08T16:00:00.0000000Z'
seed_assignment 'kandy-landslide-march' "$SUB_C2"     "$NAME_C2"     "$EMAIL_C2"     'active' '2026-03-23T08:30:00.0000000Z' '2026-04-10T10:00:00.0000000Z'
seed_assignment 'kandy-landslide-march' "$SUB_C7"     "$NAME_C7"     "$EMAIL_C7"     'active' '2026-03-24T09:00:00.0000000Z' '2026-04-10T10:00:00.0000000Z'

# Disaster 3 — Galle storm (active): 2 pledges active
seed_assignment 'galle-storm-march' "$SUB_MEDIC" "$NAME_MEDIC" "$EMAIL_MEDIC" 'active' '2026-03-30T08:00:00.0000000Z' '2026-04-14T10:00:00.0000000Z'
seed_assignment 'galle-storm-march' "$SUB_C3"    "$NAME_C3"    "$EMAIL_C3"    'active' '2026-03-30T10:00:00.0000000Z' '2026-04-14T10:00:00.0000000Z'

# Disaster 4 — Jaffna fire (active): 2 pledges (just pledged)
seed_assignment 'jaffna-market-fire' "$SUB_HM" "$NAME_HM" "$EMAIL_HM" 'pledged' '2026-04-02T11:00:00.0000000Z' ''
seed_assignment 'jaffna-market-fire' "$SUB_C4" "$NAME_C4" "$EMAIL_C4" 'pledged' '2026-04-03T08:00:00.0000000Z' ''

# Disaster 5 — Matara flood (active): 2 pledges
seed_assignment 'matara-flood-april' "$SUB_C5"    "$NAME_C5"    "$EMAIL_C5"    'active'  '2026-04-06T06:00:00.0000000Z' '2026-04-15T09:00:00.0000000Z'
seed_assignment 'matara-flood-april' "$SUB_MEDIC" "$NAME_MEDIC" "$EMAIL_MEDIC" 'pledged' '2026-04-06T15:00:00.0000000Z' ''

# Disaster 6 — Colombo tree fall (active): 2 pledges
seed_assignment 'colombo-tree-fall' "$SUB_C1" "$NAME_C1" "$EMAIL_C1" 'active'  '2026-04-08T18:00:00.0000000Z' '2026-04-13T11:00:00.0000000Z'
seed_assignment 'colombo-tree-fall' "$SUB_C6" "$NAME_C6" "$EMAIL_C6" 'pledged' '2026-04-09T07:00:00.0000000Z' ''

# -----------------------------------------------------------------------------
# Seed donations
# -----------------------------------------------------------------------------
# Args: disasterSlug donationId userSub userName itemType quantity note createdAt
seed_donation() {
  local slug="$1" id="$2" sub="$3" uname="$4" itype="$5" qty="$6" note="$7" created="$8"

  local item
  item="$(python3 - "$slug" "$id" "$sub" "$uname" "$itype" "$qty" "$note" "$created" <<'PY'
import json, sys
slug, did, sub, uname, itype, qty, note, created = sys.argv[1:]
item = {
    "PK":           {"S": f"DISASTER#{slug}"},
    "SK":           {"S": f"DON#{did}"},
    "entity":       {"S": "donation"},
    "id":           {"S": did},
    "disasterSlug": {"S": slug},
    "userSub":      {"S": sub},
    "userName":     {"S": uname},
    "itemType":     {"S": itype},
    "quantity":     {"N": str(qty)},
    "note":         {"S": note},
    "createdAt":    {"S": created},
}
print(json.dumps(item))
PY
)"

  aws dynamodb put-item \
    --table-name "$DONATIONS_TABLE" \
    --item "$item" \
    --profile "$PROFILE" --region "$REGION" >/dev/null

  echo "   donation $slug / $uname: $qty $itype"
}

# Disaster 1 — Colombo flood (closed)
seed_donation 'colombo-flood-march' 'don001colomboflood01' "$SUB_C1"     "$NAME_C1"     'water'   300 'Delivered from our store stock.'         '2026-03-16T10:00:00.0000000Z'
seed_donation 'colombo-flood-march' 'don002colomboflood02' "$SUB_HELPER" "$NAME_HELPER" 'shelter' 100 'Blankets — household surplus, pass on.'  '2026-03-17T09:00:00.0000000Z'

# Disaster 2 — Kandy landslide (needs-met)
seed_donation 'kandy-landslide-march' 'don003kandylan01' "$SUB_MEDIC" "$NAME_MEDIC" 'medical' 30  'Medic-grade first-aid kits, full inventory.' '2026-03-23T11:00:00.0000000Z'
seed_donation 'kandy-landslide-march' 'don004kandylan02' "$SUB_C2"    "$NAME_C2"    'water'   500 'Coordinated with neighbours.'                '2026-03-24T10:30:00.0000000Z'
seed_donation 'kandy-landslide-march' 'don005kandylan03' "$SUB_C7"    "$NAME_C7"    'shelter' 150 'Blankets — school PTA fundraiser proceeds.' '2026-03-26T14:00:00.0000000Z'

# Disaster 3 — Galle storm (active)
seed_donation 'galle-storm-march' 'don006gallestorm01' "$SUB_MEDIC" "$NAME_MEDIC" 'water'   200 'Bulk purchase from Galle co-op.' '2026-03-31T08:30:00.0000000Z'
seed_donation 'galle-storm-march' 'don007gallestorm02' "$SUB_C3"    "$NAME_C3"    'shelter' 40  'Waterproof tarps — 4x6m each.'   '2026-04-01T09:00:00.0000000Z'

# Disaster 4 — Jaffna fire (active)
seed_donation 'jaffna-market-fire' 'don008jaffnafire01' "$SUB_HM" "$NAME_HM" 'water'     100 'Shared inventory.'                              '2026-04-03T12:00:00.0000000Z'
seed_donation 'jaffna-market-fire' 'don009jaffnafire02' "$SUB_C4" "$NAME_C4" 'equipment' 10  'Fire extinguishers — 2 ABC, 8 water-based.'    '2026-04-04T09:00:00.0000000Z'

# Disaster 5 — Matara flood (active)
seed_donation 'matara-flood-april' 'don010mataraflood01' "$SUB_C5"    "$NAME_C5"    'food'    80  'Dry rations — rice, dhal, tea.'   '2026-04-06T07:00:00.0000000Z'
seed_donation 'matara-flood-april' 'don011mataraflood02' "$SUB_MEDIC" "$NAME_MEDIC" 'shelter' 120 'Blankets via Matara Medical Centre.' '2026-04-07T10:00:00.0000000Z'

# Disaster 6 — Colombo tree fall (active)
seed_donation 'colombo-tree-fall' 'don012tree01' "$SUB_C1" "$NAME_C1" 'tools' 2 'Two petrol chainsaws for the cleanup crew.' '2026-04-09T10:00:00.0000000Z'

# -----------------------------------------------------------------------------
# Seed resources (aggregated stockpile per disaster × itemType)
# Note: DynamoDB attribute is 'category' (C# field ItemType maps to 'category').
# -----------------------------------------------------------------------------
# Args: disasterSlug itemType quantity createdAt updatedAt
seed_resource() {
  local slug="$1" itype="$2" qty="$3" created="$4" updated="$5"

  local item
  item="$(python3 - "$slug" "$itype" "$qty" "$created" "$updated" <<'PY'
import json, sys
slug, itype, qty, created, updated = sys.argv[1:]
item = {
    "PK":           {"S": f"DISASTER#{slug}"},
    "SK":           {"S": f"ITEM#{itype}"},
    "entity":       {"S": "resource"},
    "disasterSlug": {"S": slug},
    "category":     {"S": itype},
    "quantity":     {"N": str(qty)},
    "createdAt":    {"S": created},
}
if updated:
    item["updatedAt"] = {"S": updated}
print(json.dumps(item))
PY
)"

  aws dynamodb put-item \
    --table-name "$RESOURCES_TABLE" \
    --item "$item" \
    --profile "$PROFILE" --region "$REGION" >/dev/null

  echo "   resource $slug / $itype: $qty"
}

# Aggregated donation totals per disaster — matches seed_donation sums.
# Disaster 1 — Colombo flood
seed_resource 'colombo-flood-march' 'water'   300 '2026-03-16T10:05:00.0000000Z' '2026-03-28T18:00:00.0000000Z'
seed_resource 'colombo-flood-march' 'shelter' 100 '2026-03-17T09:05:00.0000000Z' '2026-03-28T18:00:00.0000000Z'

# Disaster 2 — Kandy landslide
seed_resource 'kandy-landslide-march' 'medical' 30  '2026-03-23T11:05:00.0000000Z' '2026-04-10T12:00:00.0000000Z'
seed_resource 'kandy-landslide-march' 'water'   500 '2026-03-24T10:35:00.0000000Z' '2026-04-10T12:00:00.0000000Z'
seed_resource 'kandy-landslide-march' 'shelter' 150 '2026-03-26T14:05:00.0000000Z' '2026-04-10T12:00:00.0000000Z'

# Disaster 3 — Galle storm
seed_resource 'galle-storm-march' 'water'   200 '2026-03-31T08:35:00.0000000Z' '2026-04-15T09:00:00.0000000Z'
seed_resource 'galle-storm-march' 'shelter' 40  '2026-04-01T09:05:00.0000000Z' '2026-04-15T09:00:00.0000000Z'

# Disaster 4 — Jaffna fire
seed_resource 'jaffna-market-fire' 'water'     100 '2026-04-03T12:05:00.0000000Z' '2026-04-16T10:00:00.0000000Z'
seed_resource 'jaffna-market-fire' 'equipment' 10  '2026-04-04T09:05:00.0000000Z' '2026-04-16T10:00:00.0000000Z'

# Disaster 5 — Matara flood
seed_resource 'matara-flood-april' 'food'    80  '2026-04-06T07:05:00.0000000Z' '2026-04-17T08:00:00.0000000Z'
seed_resource 'matara-flood-april' 'shelter' 120 '2026-04-07T10:05:00.0000000Z' '2026-04-17T08:00:00.0000000Z'
seed_resource 'matara-flood-april' 'water'   0   '2026-04-05T22:05:00.0000000Z' ''

# Disaster 6 — Colombo tree fall
seed_resource 'colombo-tree-fall' 'tools'     2 '2026-04-09T10:05:00.0000000Z' '2026-04-13T11:00:00.0000000Z'
seed_resource 'colombo-tree-fall' 'equipment' 0 '2026-04-08T16:50:00.0000000Z' ''

echo
echo "Dataset seed complete."
echo "  7 citizens + 5 existing staff users"
echo "  9 disasters (2 pending, 1 rejected, 4 active, 1 needs-met, 1 closed)"
echo "  13 assignments, 12 donations, 13 resource rows"
