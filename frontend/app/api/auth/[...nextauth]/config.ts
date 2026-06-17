import CredentialsProvider from "next-auth/providers/credentials";
import type { NextAuthOptions as AuthOptions, SessionStrategy } from "next-auth";
import {
  AuthenticationDetails,
  CognitoRefreshToken,
  CognitoUser,
  CognitoUserSession,
} from "amazon-cognito-identity-js";
import { createUserPool } from "@/utils/amplify/amplify-server-utils";
import type { TokenData } from "@/types/next-auth";

const JWT_SECRET = process.env.JWT_SECRET!;

const populateTokenData = (data: CognitoUserSession): TokenData => ({
  idToken: data.getIdToken().getJwtToken(),
  accessToken: data.getAccessToken().getJwtToken(),
  refreshToken: data.getRefreshToken().getToken(),
  idTokenExpires: data.getIdToken().getExpiration() * 1000,
  accessTokenExpires: data.getAccessToken().getExpiration() * 1000,
});

export const NextAuthOptions: AuthOptions = {
  providers: [
    CredentialsProvider({
      name: "Credentials",
      credentials: {
        username: { label: "Username", type: "text" },
        password: { label: "Password", type: "password" },
        newPassword: { label: "New Password", type: "password" },
        fullName: { label: "Full Name", type: "text" },
      },
      async authorize(credentials) {
        if (!credentials) {
          throw new Error("No credentials provided");
        }

        const userPool = await createUserPool();
        const cognitoUser = new CognitoUser({
          Username: credentials.username,
          Pool: userPool,
        });

        const authenticationDetails = new AuthenticationDetails({
          Username: credentials.username,
          Password: credentials.password,
        });

        return new Promise((resolve, reject) => {
          cognitoUser.authenticateUser(authenticationDetails, {
            onSuccess: (result: CognitoUserSession) => {
              return resolve({
                id: result.getIdToken().payload.sub,
                email: result.getIdToken().payload.email,
                username: result.getIdToken().payload.name,
                tokenData: populateTokenData(result),
              });
            },

            onFailure: (err) => {
              if (err.code === "PasswordResetRequiredException") {
                return reject({
                  code: "PasswordResetRequiredException",
                  message: "Password reset required. Please contact an administrator.",
                });
              }

              if (err.code === "UserNotFoundException") {
                return reject({
                  code: "UserNotFoundException",
                  message: "User does not exist. Please contact an administrator.",
                });
              }

              return reject({
                code: err.code || "AuthenticationError",
                message: err.message || "Failed to authenticate user",
              });
            },

            newPasswordRequired: function (userAttributes) {
              if (!credentials.newPassword) {
                return reject({
                  code: "NewPasswordRequiredException",
                  message: "New Password is required",
                });
              }

              if (!credentials.fullName) {
                return reject({
                  code: "NameRequiredException",
                  message: "Name is required",
                });
              }

              delete userAttributes.email_verified;
              delete userAttributes.email;
              userAttributes.name = credentials.fullName;

              cognitoUser.completeNewPasswordChallenge(
                credentials.newPassword,
                userAttributes,
                this
              );
            },
          });
        });
      },
    }),
  ],
  pages: {
    signIn: "/signin",
    signOut: "/signout",
  },
  secret: JWT_SECRET,
  session: {
    strategy: "jwt" as SessionStrategy,
  },
  debug: false,
  callbacks: {
    async jwt({ token, user }) {
      if (user) {
        return {
          sub: user.id,
          tokenData: user.tokenData,
          email: user.email,
          username: user.username,
        };
      }

      if (
        token.tokenData &&
        Date.now() < token.tokenData.accessTokenExpires
      ) {
        return token;
      }

      try {
        const userPool = await createUserPool();
        const cognitoUser = new CognitoUser({
          Username: token.email!,
          Pool: userPool,
        });

        const refreshToken = new CognitoRefreshToken({
          RefreshToken: token.tokenData!.refreshToken,
        });

        const refreshedSession = await new Promise<CognitoUserSession>(
          (resolve, reject) => {
            cognitoUser.refreshSession(refreshToken, (err, session) => {
              if (err) return reject(err);
              return resolve(session);
            });
          }
        );

        return {
          ...token,
          tokenData: populateTokenData(refreshedSession),
        };
      } catch {
        return {
          ...token,
          error: "RefreshAccessTokenError",
        };
      }
    },

    async session({ session, token }) {
      return {
        ...session,
        user: {
          email: token.email,
          username: token.username,
          sub: token.sub,
        },
        tokenData: token.tokenData,
        expires: session.expires,
        error: token.error,
      };
    },
  },
};
