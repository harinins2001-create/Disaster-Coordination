"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import {
  ActionIcon,
  Alert,
  Badge,
  Button,
  FileInput,
  Group,
  Loader,
  Modal,
  NumberInput,
  Paper,
  Select,
  Stack,
  Table,
  Text,
  TextInput,
  Textarea,
} from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { useForm } from "@mantine/form";
import Link from "next/link";
import { BarChart3, Pencil, Plus, Trash2 } from "lucide-react";
import {
  ITEM_TYPES,
  ITEM_TYPE_LABELS,
  deleteDisaster,
  fetchAllDisasters,
  fetchDisasters,
  submitDisaster,
  updateDisaster,
  type Disaster,
  type DisasterEditInput,
  type ItemType,
  type RequiredResource,
} from "@/lib/api";
import { useProfile } from "@/hooks/useProfile";

const SEVERITY_OPTIONS = [
  { value: "low", label: "Low" },
  { value: "medium", label: "Medium" },
  { value: "high", label: "High" },
  { value: "critical", label: "Critical" },
];

const STATUS_OPTIONS = [
  { value: "active", label: "Active" },
  { value: "monitoring", label: "Monitoring" },
  { value: "needs-met", label: "Needs met" },
  { value: "resolved", label: "Resolved" },
  { value: "closed", label: "Closed" },
];

const ITEM_TYPE_OPTIONS = ITEM_TYPES.map((t) => ({
  value: t,
  label: ITEM_TYPE_LABELS[t as ItemType],
}));

const severityColor = (severity: string) => {
  switch (severity?.toLowerCase()) {
    case "critical":
      return "red";
    case "high":
      return "orange";
    case "medium":
      return "yellow";
    case "low":
      return "blue";
    default:
      return "gray";
  }
};

const statusColor = (status: string) => {
  switch (status?.toLowerCase()) {
    case "needs-met":
      return "green";
    case "active":
      return "red";
    case "monitoring":
      return "yellow";
    case "resolved":
    case "closed":
      return "gray";
    default:
      return "blue";
  }
};

type FormValues = {
  title: string;
  description: string;
  severity: string;
  location: string;
  status: string;
  requiredVolunteers: number;
  requiredResources: RequiredResource[];
};

const emptyInput = (): FormValues => ({
  title: "",
  description: "",
  severity: "medium",
  location: "",
  status: "active",
  requiredVolunteers: 0,
  requiredResources: [],
});

export default function DisastersManager() {
  const { profile } = useProfile();

  const canCreate = useMemo(
    () => !!profile && profile.active,
    [profile],
  );

  const isAdminOrMod = useMemo(
    () =>
      !!profile &&
      (profile.roles.includes("admin") || profile.roles.includes("moderator")),
    [profile],
  );

  const [disasters, setDisasters] = useState<Disaster[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);
  const [editing, setEditing] = useState<Disaster | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [photos, setPhotos] = useState<File[]>([]);
  const [opened, { open, close }] = useDisclosure(false);

  const form = useForm<FormValues>({
    initialValues: emptyInput(),
    validate: {
      title: (v) => (v.trim().length === 0 ? "Title is required" : null),
      description: (v, values) =>
        !editing && v.trim().length === 0 ? "Description is required" : null,
      location: (v, values) =>
        !editing && v.trim().length === 0 ? "Location is required" : null,
      severity: (v) => (v ? null : "Severity is required"),
      requiredVolunteers: (v) => (v < 0 ? "Must be non-negative" : null),
    },
  });

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const items = isAdminOrMod
        ? await fetchAllDisasters()
        : await fetchDisasters();
      setDisasters(items);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load disasters");
    } finally {
      setLoading(false);
    }
  }, [isAdminOrMod]);

  useEffect(() => {
    load();
  }, [load]);

  const canEdit = (_d: Disaster) => {
    if (!profile || !profile.active) return false;
    return (
      profile.roles.includes("admin") || profile.roles.includes("moderator")
    );
  };

  const canDeleteRow = (d: Disaster) => {
    if (!profile || !profile.active) return false;
    if (
      profile.roles.includes("admin") ||
      profile.roles.includes("moderator")
    ) {
      return true;
    }
    return d.reportedBySub === profile.sub && d.status === "pending";
  };

  const openNew = () => {
    setEditing(null);
    form.setValues(emptyInput());
    form.resetDirty();
    setPhotos([]);
    open();
  };

  const openEdit = (d: Disaster) => {
    setEditing(d);
    form.setValues({
      title: d.title ?? "",
      description: d.description ?? "",
      severity: d.severity ?? "medium",
      location: d.location ?? "",
      status: d.status ?? "active",
      requiredVolunteers: d.requiredVolunteers ?? 0,
      requiredResources: (d.requiredResources ?? []).map((r) => ({
        itemType: r.itemType,
        quantity: r.quantity,
      })),
    });
    form.resetDirty();
    setPhotos([]);
    open();
  };

  const addRequirementRow = () => {
    form.setFieldValue("requiredResources", [
      ...form.values.requiredResources,
      { itemType: "food", quantity: 1 },
    ]);
  };

  const updateRequirementRow = (
    idx: number,
    patch: Partial<RequiredResource>,
  ) => {
    const next = form.values.requiredResources.map((r, i) =>
      i === idx ? { ...r, ...patch } : r,
    );
    form.setFieldValue("requiredResources", next);
  };

  const removeRequirementRow = (idx: number) => {
    const next = form.values.requiredResources.filter((_, i) => i !== idx);
    form.setFieldValue("requiredResources", next);
  };

  const onSubmit = form.onSubmit(async (values) => {
    setSubmitting(true);
    setError(null);
    try {
      const cleanResources = values.requiredResources
        .map((r) => ({
          itemType: r.itemType,
          quantity: Math.max(0, Math.floor(r.quantity)),
        }))
        .filter((r) => r.itemType && r.quantity > 0);

      if (editing) {
        const patch: DisasterEditInput = {
          title: values.title,
          description: values.description,
          severity: values.severity,
          location: values.location,
          status: values.status,
          requiredVolunteers: Math.max(
            0,
            Math.floor(values.requiredVolunteers),
          ),
          requiredResources: cleanResources,
        };
        await updateDisaster(editing.slug, patch);
      } else {
        if (photos.length < 1) {
          setError("At least 1 photo is required");
          setSubmitting(false);
          return;
        }
        if (photos.length > 10) {
          setError("Maximum 10 photos allowed");
          setSubmitting(false);
          return;
        }
        await submitDisaster({
          title: values.title,
          description: values.description,
          location: values.location,
          severity: values.severity,
          requiredVolunteers: Math.max(
            0,
            Math.floor(values.requiredVolunteers),
          ),
          requiredResources: cleanResources,
          photos,
        });
        if (!isAdminOrMod) {
          setSuccessMsg(
            "Report submitted for review. You can track its status in My submissions.",
          );
        }
      }
      close();
      await load();
    } catch (e) {
      const msg =
        (e as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ??
        (e instanceof Error ? e.message : "Save failed");
      setError(msg);
    } finally {
      setSubmitting(false);
    }
  });

  const onDelete = async (d: Disaster) => {
    if (!confirm(`Delete "${d.title}"? This cannot be undone.`)) return;
    setError(null);
    try {
      await deleteDisaster(d.slug);
      await load();
    } catch (e) {
      const msg =
        (e as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ?? (e instanceof Error ? e.message : "Delete failed");
      setError(msg);
    }
  };

  return (
    <>
      <Stack gap="md">
        <Group justify="space-between" align="center">
          <Text c="gray.7" size="sm">
            {loading
              ? "Loading…"
              : `${disasters.length} disaster${disasters.length === 1 ? "" : "s"}`}
          </Text>
          {canCreate && (
            <Button leftSection={<Plus size={16} />} onClick={openNew}>
              {isAdminOrMod ? "New disaster" : "Submit a disaster"}
            </Button>
          )}
        </Group>

        {error && (
          <Alert color="red" variant="light" title="Error">
            {error}
          </Alert>
        )}

        {successMsg && (
          <Alert
            color="green"
            variant="light"
            title="Submitted"
            withCloseButton
            onClose={() => setSuccessMsg(null)}
          >
            <Group justify="space-between" align="center" wrap="nowrap">
              <Text size="sm">{successMsg}</Text>
              <Button
                component={Link}
                href="/profile/submissions"
                size="xs"
                variant="light"
              >
                View submissions
              </Button>
            </Group>
          </Alert>
        )}

        <Paper shadow="xs" radius="md" withBorder>
          {loading ? (
            <Group p="md">
              <Loader size="sm" />
              <Text>Loading disasters…</Text>
            </Group>
          ) : disasters.length === 0 ? (
            <Text p="md" c="gray.7">
              No disasters yet.
            </Text>
          ) : (
            <Table striped highlightOnHover verticalSpacing="sm">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Title</Table.Th>
                  <Table.Th>Severity</Table.Th>
                  <Table.Th>Location</Table.Th>
                  <Table.Th>Status</Table.Th>
                  <Table.Th>Needs</Table.Th>
                  <Table.Th>Submitted by</Table.Th>
                  <Table.Th style={{ width: 140 }}>Actions</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {disasters.map((d) => (
                  <Table.Tr key={d.slug}>
                    <Table.Td>
                      <Text fw={500}>{d.title}</Text>
                      <Text size="xs" c="gray.7" lineClamp={1}>
                        {d.description}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Badge color={severityColor(d.severity)} variant="light">
                        {d.severity || "—"}
                      </Badge>
                    </Table.Td>
                    <Table.Td>{d.location || "—"}</Table.Td>
                    <Table.Td>
                      <Badge color={statusColor(d.status)} variant="light">
                        {d.status || "—"}
                      </Badge>
                    </Table.Td>
                    <Table.Td>
                      <Text size="xs">Vol: {d.requiredVolunteers ?? 0}</Text>
                      <Text size="xs" c="gray.7">
                        Items: {d.requiredResources?.length ?? 0}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm" c="gray.7">
                        {d.reportedByName || d.reportedBy || "—"}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Group gap="xs">
                        <ActionIcon
                          component={Link}
                          href={`/disasters/${d.slug}`}
                          variant="subtle"
                          color="blue"
                          aria-label="Resources"
                        >
                          <BarChart3 size={16} />
                        </ActionIcon>
                        {canEdit(d) && (
                          <ActionIcon
                            variant="subtle"
                            onClick={() => openEdit(d)}
                            aria-label="Edit"
                          >
                            <Pencil size={16} />
                          </ActionIcon>
                        )}
                        {canDeleteRow(d) && (
                          <ActionIcon
                            variant="subtle"
                            color="red"
                            onClick={() => onDelete(d)}
                            aria-label="Delete"
                          >
                            <Trash2 size={16} />
                          </ActionIcon>
                        )}
                      </Group>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          )}
        </Paper>
      </Stack>

      <Modal
        opened={opened}
        onClose={close}
        title={editing ? `Edit: ${editing.title}` : "Submit a disaster"}
        size="lg"
        centered
      >
        <form onSubmit={onSubmit}>
          <Stack gap="sm">
            {!editing && (
              <Alert color="blue" variant="light">
                {isAdminOrMod
                  ? "As an admin/moderator your submission will be published immediately."
                  : "Your submission will be reviewed by a moderator before being published."}
              </Alert>
            )}
            <TextInput
              label="Title"
              placeholder="Chemistry lab fire"
              required
              {...form.getInputProps("title")}
            />
            <Textarea
              label="Description"
              placeholder="What happened?"
              minRows={3}
              autosize
              required={!editing}
              {...form.getInputProps("description")}
            />
            <Group grow>
              <Select
                label="Severity"
                data={SEVERITY_OPTIONS}
                required
                {...form.getInputProps("severity")}
              />
              {editing && (
                <Select
                  label="Status"
                  data={STATUS_OPTIONS}
                  required
                  {...form.getInputProps("status")}
                />
              )}
            </Group>
            <TextInput
              label="Location"
              placeholder="Block B, 3rd floor"
              required={!editing}
              {...form.getInputProps("location")}
            />
            {!editing && (
              <FileInput
                label="Photos (1–10, max 5MB each)"
                placeholder="Choose image(s)"
                accept="image/jpeg,image/png,image/webp,image/gif,image/heic"
                multiple
                value={photos}
                onChange={(v) => setPhotos(v ?? [])}
                clearable
                required
              />
            )}
            <NumberInput
              label="Required volunteers"
              min={0}
              {...form.getInputProps("requiredVolunteers")}
            />

            <Stack gap="xs">
              <Group justify="space-between" align="end">
                <Text fw={500} size="sm">
                  Required resources
                </Text>
                <Button size="xs" variant="light" onClick={addRequirementRow}>
                  Add item
                </Button>
              </Group>
              {form.values.requiredResources.length === 0 && (
                <Text size="xs" c="dimmed">
                  No required items — add some to track donation progress.
                </Text>
              )}
              {form.values.requiredResources.map((r, idx) => (
                <Group key={idx} gap="xs" align="end">
                  <Select
                    label={idx === 0 ? "Item" : undefined}
                    data={ITEM_TYPE_OPTIONS}
                    value={r.itemType}
                    onChange={(v) =>
                      updateRequirementRow(idx, { itemType: v ?? "other" })
                    }
                    style={{ flex: 1 }}
                  />
                  <NumberInput
                    label={idx === 0 ? "Quantity" : undefined}
                    min={1}
                    value={r.quantity}
                    onChange={(v) =>
                      updateRequirementRow(idx, {
                        quantity: typeof v === "number" ? v : 1,
                      })
                    }
                    style={{ width: 120 }}
                  />
                  <ActionIcon
                    color="red"
                    variant="subtle"
                    onClick={() => removeRequirementRow(idx)}
                    aria-label="Remove"
                  >
                    <Trash2 size={16} />
                  </ActionIcon>
                </Group>
              ))}
            </Stack>

            <Group justify="flex-end" mt="sm">
              <Button variant="subtle" onClick={close} disabled={submitting}>
                Cancel
              </Button>
              <Button type="submit" loading={submitting}>
                {editing ? "Save changes" : "Submit"}
              </Button>
            </Group>
          </Stack>
        </form>
      </Modal>
    </>
  );
}
