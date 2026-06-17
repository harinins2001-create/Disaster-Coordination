"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import Image from "next/image";
import {
  Alert,
  Badge,
  Button,
  Container,
  Group,
  Loader,
  Modal,
  Paper,
  Stack,
  Text,
  Textarea,
  Title,
} from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { Check, X } from "lucide-react";
import SiteHeader from "@/components/SiteHeader";
import { useProfile } from "@/hooks/useProfile";
import {
  approveDisaster,
  fetchPendingDisasters,
  rejectDisaster,
  type Disaster,
} from "@/lib/api";

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

export default function ReviewQueuePage() {
  const { profile, loading: profileLoading } = useProfile();
  const [items, setItems] = useState<Disaster[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busySlug, setBusySlug] = useState<string | null>(null);

  const [rejectOpen, { open: openReject, close: closeReject }] =
    useDisclosure(false);
  const [rejectTarget, setRejectTarget] = useState<Disaster | null>(null);
  const [rejectReason, setRejectReason] = useState("");
  const [rejectSubmitting, setRejectSubmitting] = useState(false);

  const isAdminOrMod =
    !!profile &&
    profile.active &&
    (profile.roles.includes("admin") || profile.roles.includes("moderator"));

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const list = await fetchPendingDisasters();
      setItems(list);
    } catch (err) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ?? "Failed to load pending submissions";
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!profileLoading && isAdminOrMod) load();
    else if (!profileLoading) setLoading(false);
  }, [profileLoading, isAdminOrMod, load]);

  const onApprove = async (d: Disaster) => {
    setBusySlug(d.slug);
    setError(null);
    try {
      await approveDisaster(d.slug);
      await load();
    } catch (err) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ?? "Approval failed";
      setError(msg);
    } finally {
      setBusySlug(null);
    }
  };

  const openRejectModal = (d: Disaster) => {
    setRejectTarget(d);
    setRejectReason("");
    openReject();
  };

  const onReject = async () => {
    if (!rejectTarget) return;
    if (!rejectReason.trim()) {
      setError("Reason is required");
      return;
    }
    setRejectSubmitting(true);
    setError(null);
    try {
      await rejectDisaster(rejectTarget.slug, rejectReason.trim());
      closeReject();
      setRejectTarget(null);
      setRejectReason("");
      await load();
    } catch (err) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ?? "Rejection failed";
      setError(msg);
    } finally {
      setRejectSubmitting(false);
    }
  };

  if (!profileLoading && !isAdminOrMod) {
    return (
      <div className="min-h-screen bg-white">
        <SiteHeader />
        <Container size="md" py="xl">
          <Alert color="red" variant="light" title="Forbidden">
            Only administrators and moderators can view the review queue.
          </Alert>
        </Container>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-white">
      <SiteHeader />
      <Container size="lg" py="xl">
        <Stack gap="lg">
          <Group justify="space-between" align="center">
            <div>
              <Title order={2}>Disaster review queue</Title>
              <Text c="gray.7" size="sm" mt={4}>
                {loading
                  ? "Loading…"
                  : `${items.length} pending submission${items.length === 1 ? "" : "s"}`}
              </Text>
            </div>
            <Button component={Link} href="/admin" variant="subtle" size="sm">
              Back to admin
            </Button>
          </Group>

          {error && (
            <Alert color="red" variant="light">
              {error}
            </Alert>
          )}

          {loading ? (
            <Group>
              <Loader size="sm" />
              <Text>Loading pending submissions…</Text>
            </Group>
          ) : items.length === 0 ? (
            <Paper withBorder radius="md" p="md">
              <Text c="dimmed">No pending submissions to review.</Text>
            </Paper>
          ) : (
            <Stack gap="md">
              {items.map((d) => (
                <Paper key={d.slug} withBorder radius="md" p="md" shadow="xs">
                  <Stack gap="sm">
                    <Group justify="space-between" align="flex-start">
                      <div>
                        <Group gap="xs" align="center">
                          <Title order={4}>{d.title}</Title>
                          <Badge color={severityColor(d.severity)} variant="light">
                            {d.severity || "—"}
                          </Badge>
                        </Group>
                        <Text size="sm" c="gray.7" mt={2}>
                          {d.location || "—"}
                        </Text>
                        <Text size="xs" c="dimmed" mt={2}>
                          Submitted by{" "}
                          {d.reportedByName || d.reportedBy || "—"}
                          {d.createdAt ? ` · ${d.createdAt}` : ""}
                        </Text>
                      </div>
                      <Group gap="xs">
                        <Button
                          size="sm"
                          color="green"
                          leftSection={<Check size={16} />}
                          loading={busySlug === d.slug}
                          onClick={() => onApprove(d)}
                        >
                          Approve
                        </Button>
                        <Button
                          size="sm"
                          color="red"
                          variant="light"
                          leftSection={<X size={16} />}
                          onClick={() => openRejectModal(d)}
                        >
                          Reject
                        </Button>
                      </Group>
                    </Group>

                    <Text size="sm">{d.description}</Text>

                    {d.photoUrls && d.photoUrls.length > 0 && (
                      <Group gap="xs" wrap="wrap">
                        {d.photoUrls.map((url, idx) => (
                          <div
                            key={idx}
                            style={{
                              position: "relative",
                              width: 160,
                              height: 120,
                              borderRadius: 8,
                              overflow: "hidden",
                              border: "1px solid #e9ecef",
                            }}
                          >
                            <Image
                              src={url}
                              alt={`${d.title} photo ${idx + 1}`}
                              fill
                              sizes="160px"
                              style={{ objectFit: "cover" }}
                              unoptimized
                            />
                          </div>
                        ))}
                      </Group>
                    )}

                    {(d.requiredVolunteers ?? 0) > 0 ||
                    (d.requiredResources?.length ?? 0) > 0 ? (
                      <Group gap="md">
                        <Text size="xs" c="gray.7">
                          Volunteers requested: {d.requiredVolunteers ?? 0}
                        </Text>
                        <Text size="xs" c="gray.7">
                          Items requested: {d.requiredResources?.length ?? 0}
                        </Text>
                      </Group>
                    ) : null}
                  </Stack>
                </Paper>
              ))}
            </Stack>
          )}
        </Stack>
      </Container>

      <Modal
        opened={rejectOpen}
        onClose={closeReject}
        title={rejectTarget ? `Reject: ${rejectTarget.title}` : "Reject"}
        centered
      >
        <Stack gap="sm">
          <Textarea
            label="Reason"
            placeholder="Explain why this submission is being rejected. The submitter will see this message."
            minRows={4}
            autosize
            required
            value={rejectReason}
            onChange={(e) => setRejectReason(e.currentTarget.value)}
          />
          <Group justify="flex-end">
            <Button variant="subtle" onClick={closeReject} disabled={rejectSubmitting}>
              Cancel
            </Button>
            <Button color="red" onClick={onReject} loading={rejectSubmitting}>
              Reject submission
            </Button>
          </Group>
        </Stack>
      </Modal>
    </div>
  );
}
