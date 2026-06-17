"use client";

import { Suspense, useState } from "react";
import Link from "next/link";
import { signIn } from "next-auth/react";
import { useRouter, useSearchParams } from "next/navigation";
import {
  Button,
  Paper,
  PasswordInput,
  Stack,
  Text,
  TextInput,
  Title,
  Alert,
} from "@mantine/core";
import { useForm } from "@mantine/form";
import { LifeBuoy } from "lucide-react";

export default function SignInPage() {
  return (
    <Suspense fallback={null}>
      <SignInForm />
    </Suspense>
  );
}

function SignInForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const callbackUrl = searchParams.get("callbackUrl") ?? "/dashboard";

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const form = useForm({
    initialValues: { username: "", password: "" },
    validate: {
      username: (v) => (v.length === 0 ? "Email is required" : null),
      password: (v) => (v.length === 0 ? "Password is required" : null),
    },
  });

  const handleSubmit = async (values: typeof form.values) => {
    setLoading(true);
    setError(null);

    const result = await signIn("credentials", {
      username: values.username,
      password: values.password,
      redirect: false,
    });

    setLoading(false);

    if (result?.error) {
      setError(result.error);
      return;
    }

    router.push(callbackUrl);
    router.refresh();
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-50 p-4">
      <div className="w-full max-w-md">
        <div className="mb-6 flex flex-col items-center gap-2">
          <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-blue-600 text-white shadow-sm">
            <LifeBuoy size={20} strokeWidth={2.2} />
          </span>
          <Text size="xs" c="gray.6" fw={500} className="tracking-wide uppercase">
            Disaster Response Coordination
          </Text>
        </div>
        <Paper shadow="md" p="xl" radius="md" withBorder>
          <Stack gap="lg">
            <Stack gap={2} align="center">
              <Title order={2} ta="center">
                Welcome back
              </Title>
              <Text size="sm" c="gray.6" ta="center">
                Sign in to continue to DRCS.
              </Text>
            </Stack>

            {error && (
              <Alert color="red" variant="light">
                {error}
              </Alert>
            )}

            <form onSubmit={form.onSubmit(handleSubmit)}>
              <Stack gap="md">
                <TextInput
                  label="Email"
                  placeholder="youremail@gmail.com"
                  required
                  autoComplete="username"
                  {...form.getInputProps("username")}
                />
                <PasswordInput
                  label="Password"
                  placeholder="Your password"
                  required
                  autoComplete="current-password"
                  {...form.getInputProps("password")}
                />
                <Button type="submit" fullWidth loading={loading}>
                  Sign in
                </Button>
              </Stack>
            </form>

            <Text size="sm" ta="center" c="dimmed">
              Don&apos;t have an account?{" "}
              <Link href="/signup" className="underline">
                Create one
              </Link>
            </Text>
          </Stack>
        </Paper>
      </div>
    </div>
  );
}
