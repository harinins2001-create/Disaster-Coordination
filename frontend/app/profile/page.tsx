"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import {
  Alert,
  Avatar,
  Badge,
  Button,
  Container,
  FileInput,
  Group,
  Loader,
  MultiSelect,
  Paper,
  Select,
  Stack,
  Text,
  Title,
} from "@mantine/core";
import { useForm } from "@mantine/form";
import SiteHeader from "@/components/SiteHeader";
import { useProfile } from "@/hooks/useProfile";
import {
  SKILL_OPTIONS,
  SRI_LANKAN_DISTRICTS,
  TRAVEL_OPTIONS,
  updateMe,
  uploadMyPhoto,
} from "@/lib/api";

const toOptions = (arr: readonly string[]) =>
  arr.map((v) => ({ value: v, label: v.replace(/-/g, " ") }));

type EditablePatch = {
  area: string;
  skills: string[];
  travelMethods: string[];
};

export default function ProfilePage() {
  const { profile, loading, error, refresh } = useProfile();
  const [submitting, setSubmitting] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [photoFile, setPhotoFile] = useState<File | null>(null);
  const [photoUploading, setPhotoUploading] = useState(false);
  const [photoError, setPhotoError] = useState<string | null>(null);
  const [photoSaved, setPhotoSaved] = useState(false);

  const form = useForm<EditablePatch>({
    initialValues: {
      area: "",
      skills: [],
      travelMethods: [],
    },
  });

  useEffect(() => {
    if (profile) {
      form.setValues({
        area: profile.area ?? "",
        skills: profile.skills ?? [],
        travelMethods: profile.travelMethods ?? [],
      });
      form.resetDirty();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [profile?.sub]);

  const onSubmit = form.onSubmit(async (values) => {
    setSubmitting(true);
    setSaveError(null);
    setSaved(false);
    try {
      await updateMe(values);
      await refresh();
      setSaved(true);
    } catch (err) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ?? "Save failed";
      setSaveError(msg);
    } finally {
      setSubmitting(false);
    }
  });

  const onUploadPhoto = async () => {
    if (!photoFile) return;
    setPhotoUploading(true);
    setPhotoError(null);
    setPhotoSaved(false);
    try {
      await uploadMyPhoto(photoFile);
      await refresh();
      setPhotoFile(null);
      setPhotoSaved(true);
    } catch (err) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ?? "Upload failed";
      setPhotoError(msg);
    } finally {
      setPhotoUploading(false);
    }
  };

  return (
    <div className="min-h-screen bg-white">
      <SiteHeader />
      <Container size="md" py={48} px="md">
        <Stack gap="xl">
          <Group justify="space-between" align="center" wrap="wrap">
            <Stack gap={4}>
              <Title order={2}>My profile</Title>
              <Text c="gray.6" size="sm">
                Manage your details, skills, and contact info.
              </Text>
            </Stack>
            <Group gap="xs">
              <Button
                component={Link}
                href="/profile/submissions"
                variant="light"
                size="sm"
              >
                Submissions
              </Button>
              <Button
                component={Link}
                href="/profile/assignments"
                variant="light"
                size="sm"
              >
                Assignments
              </Button>
              <Button
                component={Link}
                href="/profile/donations"
                variant="light"
                size="sm"
              >
                Donations
              </Button>
            </Group>
          </Group>

          {loading && (
            <Group>
              <Loader size="sm" />
              <Text>Loading profile…</Text>
            </Group>
          )}

          {error && (
            <Alert color="red" variant="light">
              {error}
            </Alert>
          )}

          {profile && (
            <>
              <Paper withBorder radius="md" p="xl">
                <Group align="flex-start" gap="xl" wrap="nowrap">
                  <Avatar
                    src={profile.photoUrl ?? undefined}
                    alt={profile.name}
                    size={96}
                    radius="xl"
                    color="blue"
                  >
                    {(profile.name ?? "?").charAt(0).toUpperCase()}
                  </Avatar>
                  <Stack gap={6} style={{ flex: 1 }}>
                    <Group gap="xs" align="center">
                      <Text fw={700} size="lg">
                        {profile.name}
                      </Text>
                      {!profile.active && (
                        <Badge color="red" variant="light" size="sm">
                          disabled
                        </Badge>
                      )}
                    </Group>
                    <Text size="sm" c="gray.6">
                      {profile.email}
                    </Text>
                    <Group gap={6} mt={2}>
                      {profile.roles.map((r) => (
                        <Badge key={r} variant="light" size="sm">
                          {r}
                        </Badge>
                      ))}
                    </Group>
                    <Group gap="xl" mt="md" wrap="wrap">
                      <Text size="sm" c="gray.7">
                        <Text span fw={600} c="gray.9">
                          NIC:
                        </Text>{" "}
                        {profile.nic}
                      </Text>
                      <Text size="sm" c="gray.7">
                        <Text span fw={600} c="gray.9">
                          Phone:
                        </Text>{" "}
                        {profile.phone}
                      </Text>
                      <Text size="sm" c="gray.7">
                        <Text span fw={600} c="gray.9">
                          DOB:
                        </Text>{" "}
                        {profile.dob}
                      </Text>
                      <Text size="sm" c="gray.7">
                        <Text span fw={600} c="gray.9">
                          Gender:
                        </Text>{" "}
                        {profile.gender}
                      </Text>
                    </Group>
                  </Stack>
                </Group>
              </Paper>

              <Paper withBorder radius="md" p="lg">
                <Stack gap="sm">
                  <Title order={4}>Profile photo</Title>
                  {photoError && (
                    <Alert color="red" variant="light">
                      {photoError}
                    </Alert>
                  )}
                  {photoSaved && (
                    <Alert color="green" variant="light">
                      Photo updated.
                    </Alert>
                  )}
                  <FileInput
                    label="Upload a new photo"
                    placeholder="Choose an image (JPEG / PNG / WebP, under 5MB)"
                    accept="image/jpeg,image/png,image/webp,image/gif,image/heic"
                    value={photoFile}
                    onChange={setPhotoFile}
                    clearable
                  />
                  <Group justify="flex-end">
                    <Button
                      onClick={onUploadPhoto}
                      disabled={!photoFile}
                      loading={photoUploading}
                    >
                      Upload photo
                    </Button>
                  </Group>
                </Stack>
              </Paper>

              <Paper withBorder radius="md" p="lg">
                <form onSubmit={onSubmit}>
                  <Stack gap="md">
                    <Title order={4}>Editable details</Title>

                    {saveError && (
                      <Alert color="red" variant="light">
                        {saveError}
                      </Alert>
                    )}
                    {saved && (
                      <Alert color="green" variant="light">
                        Profile updated.
                      </Alert>
                    )}

                    <Select
                      label="District"
                      data={SRI_LANKAN_DISTRICTS.map((d) => ({
                        value: d,
                        label: d,
                      }))}
                      searchable
                      {...form.getInputProps("area")}
                    />

                    <MultiSelect
                      label="Skills"
                      data={toOptions(SKILL_OPTIONS)}
                      searchable
                      clearable
                      {...form.getInputProps("skills")}
                    />

                    <MultiSelect
                      label="Travel methods"
                      data={toOptions(TRAVEL_OPTIONS)}
                      searchable
                      clearable
                      {...form.getInputProps("travelMethods")}
                    />

                    <Group justify="flex-end">
                      <Button type="submit" loading={submitting}>
                        Save changes
                      </Button>
                    </Group>
                  </Stack>
                </form>
              </Paper>
            </>
          )}
        </Stack>
      </Container>
    </div>
  );
}
