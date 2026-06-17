"use client";

import { useEffect, useState } from "react";
import {
  Alert,
  Badge,
  Button,
  Card,
  Container,
  Group,
  Loader,
  Modal,
  SimpleGrid,
  Stack,
  Text,
  Title,
} from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { BarChart3 } from "lucide-react";
import SiteHeader from "@/components/SiteHeader";
import ResourcesChart from "@/components/ResourcesChart";
import {
  fetchDisasters,
  fetchResources,
  type Disaster,
  type Resource,
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

export default function Home() {
  const [disasters, setDisasters] = useState<Disaster[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [opened, { open, close }] = useDisclosure(false);
  const [active, setActive] = useState<Disaster | null>(null);
  const [resources, setResources] = useState<Resource[]>([]);
  const [resourcesLoading, setResourcesLoading] = useState(false);
  const [resourcesError, setResourcesError] = useState<string | null>(null);

  useEffect(() => {
    fetchDisasters()
      .then((items) => setDisasters(items))
      .catch((e) => setError(e?.message ?? "Failed to load disasters"))
      .finally(() => setLoading(false));
  }, []);

  const openResources = async (d: Disaster) => {
    setActive(d);
    setResources([]);
    setResourcesError(null);
    open();
    setResourcesLoading(true);
    try {
      const items = await fetchResources(d.slug);
      setResources(items);
    } catch (e) {
      setResourcesError(e instanceof Error ? e.message : "Failed to load resources");
    } finally {
      setResourcesLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-white">
      <SiteHeader />

      <Container size="lg" py={48} px="md">
        <Stack gap="xl">
          <Stack gap={4}>
            <Title order={1}>Disasters</Title>
            <Text c="gray.6" size="sm">
              Live feed of reported incidents across Sri Lanka.
            </Text>
          </Stack>

          {loading && (
            <Group>
              <Loader size="sm" />
              <Text>Loading disasters…</Text>
            </Group>
          )}

          {error && (
            <Alert color="red" variant="light" title="Couldn’t load disasters">
              {error}
            </Alert>
          )}

          {!loading && !error && disasters.length === 0 && (
            <Alert color="gray" variant="light">
              No disasters reported yet.
            </Alert>
          )}

          {disasters.length > 0 && (
            <SimpleGrid cols={{ base: 1, sm: 2, md: 3 }} spacing="lg">
              {disasters.map((d) => (
                <Card
                  key={d.slug}
                  shadow="xs"
                  padding="lg"
                  radius="md"
                  withBorder
                  className="transition hover:shadow-md"
                >
                  <Stack gap="sm" justify="space-between" h="100%">
                    <Stack gap="sm">
                      <Group justify="space-between" align="flex-start" wrap="nowrap">
                        <Title order={4} className="leading-snug">
                          {d.title}
                        </Title>
                        <Badge color={severityColor(d.severity)} variant="light">
                          {d.severity || "—"}
                        </Badge>
                      </Group>
                      <Text size="sm" c="gray.7" lineClamp={3}>
                        {d.description}
                      </Text>
                      <Group gap={6} mt={4}>
                        <Badge variant="outline" size="sm" color="gray">
                          {d.location || "No location"}
                        </Badge>
                        <Badge variant="outline" size="sm" color="blue">
                          {d.status || "unknown"}
                        </Badge>
                      </Group>
                    </Stack>
                    <Button
                      variant="light"
                      size="xs"
                      mt="xs"
                      leftSection={<BarChart3 size={14} />}
                      onClick={() => openResources(d)}
                    >
                      View resources
                    </Button>
                  </Stack>
                </Card>
              ))}
            </SimpleGrid>
          )}
        </Stack>
      </Container>

      <Modal
        opened={opened}
        onClose={close}
        title={active ? `Resources — ${active.title}` : "Resources"}
        size="lg"
        centered
      >
        {active && (
          <Stack gap="sm">
            <Group gap="xs">
              <Badge color={severityColor(active.severity)} variant="light">
                {active.severity}
              </Badge>
              <Badge variant="outline">{active.status}</Badge>
              <Text size="sm" c="gray.7">
                {active.location}
              </Text>
            </Group>

            {resourcesError && (
              <Alert color="red" variant="light" title="Error">
                {resourcesError}
              </Alert>
            )}

            {resourcesLoading ? (
              <Group>
                <Loader size="sm" />
                <Text>Loading resources…</Text>
              </Group>
            ) : (
              <ResourcesChart resources={resources} />
            )}
          </Stack>
        )}
      </Modal>
    </div>
  );
}
