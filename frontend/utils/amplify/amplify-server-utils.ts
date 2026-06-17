"use server";

import { CognitoUserPool } from "amazon-cognito-identity-js";

const userPoolId = String(process.env.NEXT_PUBLIC_USER_POOL_ID);
const userPoolClientId = String(process.env.NEXT_PUBLIC_USER_POOL_CLIENT_ID);

if (!userPoolId || !userPoolClientId) {
  throw new Error(
    "Missing NEXT_PUBLIC_USER_POOL_ID or NEXT_PUBLIC_USER_POOL_CLIENT_ID"
  );
}

const poolData = {
  UserPoolId: userPoolId,
  ClientId: userPoolClientId,
};

let pool: CognitoUserPool | null = null;

const createUserPool = async () => {
  if (!pool) {
    pool = new CognitoUserPool(poolData);
  }
  return pool;
};

const getUserPool = async () => {
  if (!pool) {
    throw new Error("User Pool not initialized");
  }
  return pool;
};

export { createUserPool, getUserPool };
