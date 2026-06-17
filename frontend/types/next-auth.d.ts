import "next-auth";
import "next-auth/jwt";

export interface TokenData {
  idToken: string;
  accessToken: string;
  refreshToken: string;
  idTokenExpires: number;
  accessTokenExpires: number;
}

declare module "next-auth" {
  interface User {
    id: string;
    email: string;
    username?: string;
    tokenData: TokenData;
  }

  interface Session {
    user: {
      email?: string | null;
      username?: string | null;
      sub?: string | null;
    };
    tokenData?: TokenData;
    expires: string;
    error?: string;
  }
}

declare module "next-auth/jwt" {
  interface JWT {
    sub?: string;
    email?: string;
    username?: string;
    tokenData?: TokenData;
    error?: string;
  }
}
