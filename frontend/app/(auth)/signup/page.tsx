"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { signIn } from "next-auth/react";
import {
  Alert,
  Button,
  FileInput,
  Group,
  MultiSelect,
  Paper,
  PasswordInput,
  Select,
  Stack,
  Text,
  TextInput,
  Title,
} from "@mantine/core";
import { useForm } from "@mantine/form";
import {
  SKILL_OPTIONS,
  SRI_LANKAN_DISTRICTS,
  TRAVEL_OPTIONS,
  checkExists,
  signupPublic,
  type SignupInput,
} from "@/lib/api";

const NIC_RE = /^(\d{9}[VvXx]|\d{12})$/;
const PHONE_RE = /^\+?[0-9]{7,15}$/;

type FormValues = SignupInput & { confirmPassword: string };

const toOptions = (arr: readonly string[]) =>
  arr.map((v) => ({ value: v, label: v.replace(/-/g, " ") }));

export default function SignUpPage() {
  const router = useRouter();
  const [submitting, setSubmitting] = useState(false);
  const [serverError, setServerError] = useState<string | null>(null);

  const form = useForm<FormValues>({
    initialValues: {
      email: "",
      password: "",
      confirmPassword: "",
      name: "",
      nic: "",
      phone: "",
      dob: "",
      gender: "M",
      photoKey: "",
      area: "",
      skills: [],
      travelMethods: [],
      roles: ["helper"],
      photo: null,
    },
    validate: {
      email: (v) => (/^\S+@\S+\.\S+$/.test(v) ? null : "Valid email required"),
      password: (v) => (v.length >= 8 ? null : "At least 8 characters"),
      confirmPassword: (v, values) =>
        v === values.password ? null : "Passwords do not match",
      name: (v) => (v.trim().length >= 2 ? null : "Name is required"),
      nic: (v) => (NIC_RE.test(v) ? null : "Sri Lankan NIC (9-digit + V/X or 12 digits)"),
      phone: (v) => (PHONE_RE.test(v) ? null : "Phone number is invalid"),
      dob: (v) => (v.length === 10 ? null : "Date of birth required"),
      gender: (v) => (v === "M" || v === "F" ? null : "Select gender"),
      area: (v) => (v ? null : "District required"),
    },
  });

  const onSubmit = form.onSubmit(async (values) => {
    setSubmitting(true);
    setServerError(null);
    try {
      const exists = await checkExists({ email: values.email, nic: values.nic });
      if (exists.emailExists) {
        form.setFieldError("email", "Email already in use");
        setSubmitting(false);
        return;
      }
      if (exists.nicExists) {
        form.setFieldError("nic", "NIC already in use");
        setSubmitting(false);
        return;
      }

      const { confirmPassword: _unused, ...payload } = values;
      void _unused;

      await signupPublic(payload);

      const result = await signIn("credentials", {
        username: values.email,
        password: values.password,
        redirect: false,
      });

      if (result?.error) {
        setServerError(
          "Account created but automatic sign-in failed. Please sign in manually.",
        );
        router.push("/signin");
        return;
      }

      router.push("/dashboard");
      router.refresh();
    } catch (err) {
      const apiMessage =
        (err as { response?: { data?: { message?: string; fields?: Record<string, string> } } })
          ?.response?.data;
      if (apiMessage?.fields) {
        for (const [k, v] of Object.entries(apiMessage.fields)) {
          form.setFieldError(k, v);
        }
      }
      setServerError(apiMessage?.message ?? "Sign up failed");
    } finally {
      setSubmitting(false);
    }
  });

  return (
    <div className="flex min-h-screen items-center justify-center bg-white p-4 py-8">
      <Paper shadow="md" p="xl" radius="md" withBorder className="w-full max-w-2xl">
        <Stack gap="lg">
          <div>
            <Title order={2} ta="center">
              DRCS — Sign Up
            </Title>
            <Text ta="center" c="dimmed" size="sm" mt={4}>
              Register as a helper.
            </Text>
          </div>

          {serverError && (
            <Alert color="red" variant="light">
              {serverError}
            </Alert>
          )}

          <form onSubmit={onSubmit}>
            <Stack gap="md">
              <Group grow>
                <TextInput
                  label="Email"
                  placeholder="you@example.com"
                  required
                  autoComplete="email"
                  {...form.getInputProps("email")}
                />
                <TextInput
                  label="Full name"
                  placeholder="Kasun Perera"
                  required
                  {...form.getInputProps("name")}
                />
              </Group>

              <Group grow>
                <PasswordInput
                  label="Password"
                  placeholder="At least 8 characters"
                  required
                  autoComplete="new-password"
                  {...form.getInputProps("password")}
                />
                <PasswordInput
                  label="Confirm password"
                  required
                  autoComplete="new-password"
                  {...form.getInputProps("confirmPassword")}
                />
              </Group>

              <Group grow>
                <TextInput
                  label="NIC"
                  placeholder="200112345678 or 991234567V"
                  required
                  {...form.getInputProps("nic")}
                />
                <TextInput
                  label="Phone"
                  placeholder="+94771234567"
                  required
                  {...form.getInputProps("phone")}
                />
              </Group>

              <Group grow>
                <TextInput
                  type="date"
                  label="Date of birth"
                  required
                  {...form.getInputProps("dob")}
                />
                <Select
                  label="Gender"
                  data={[
                    { value: "M", label: "Male" },
                    { value: "F", label: "Female" },
                  ]}
                  required
                  {...form.getInputProps("gender")}
                />
              </Group>

              <Select
                label="District"
                data={SRI_LANKAN_DISTRICTS.map((d) => ({ value: d, label: d }))}
                searchable
                required
                {...form.getInputProps("area")}
              />

              <MultiSelect
                label="Skills"
                placeholder="Pick skills"
                data={toOptions(SKILL_OPTIONS)}
                searchable
                clearable
                {...form.getInputProps("skills")}
              />

              <MultiSelect
                label="Travel methods"
                placeholder="How can you travel?"
                data={toOptions(TRAVEL_OPTIONS)}
                searchable
                clearable
                {...form.getInputProps("travelMethods")}
              />

              <FileInput
                label="Profile photo (optional)"
                placeholder="Choose an image (JPEG / PNG / WebP, under 5MB)"
                accept="image/jpeg,image/png,image/webp,image/gif,image/heic"
                clearable
                {...form.getInputProps("photo")}
              />

              <Group justify="space-between" mt="sm">
                <Text size="sm" c="dimmed">
                  Already have an account?{" "}
                  <Link href="/signin" className="underline">
                    Sign in
                  </Link>
                </Text>
                <Button type="submit" loading={submitting}>
                  Create account
                </Button>
              </Group>
            </Stack>
          </form>
        </Stack>
      </Paper>
    </div>
  );
}
