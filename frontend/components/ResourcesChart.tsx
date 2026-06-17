"use client";

import dynamic from "next/dynamic";
import { Badge, Group, Paper, Stack, Text } from "@mantine/core";
import { ITEM_TYPES, ITEM_TYPE_LABELS, type Resource } from "@/lib/api";

const ApexChart = dynamic(() => import("react-apexcharts"), { ssr: false });

const ITEM_TYPE_COLORS: Record<(typeof ITEM_TYPES)[number], string> = {
  food: "#f97316",
  water: "#2563eb",
  clothes: "#8b5cf6",
  medical: "#ef4444",
  shelter: "#14b8a6",
  tools: "#64748b",
  equipment: "#4f46e5",
  other: "#94a3b8",
};

const EMPTY_COLOR = "#e2e8f0";

export function mergeWithAllTypes(resources: Resource[]) {
  const byType = new Map<string, number>();
  for (const r of resources) byType.set(r.itemType, r.quantity);
  return ITEM_TYPES.map((t) => ({
    itemType: t,
    label: ITEM_TYPE_LABELS[t],
    quantity: byType.get(t) ?? 0,
  }));
}

export default function ResourcesChart({
  resources,
  height = 300,
}: {
  resources: Resource[];
  height?: number;
}) {
  const merged = mergeWithAllTypes(resources);
  const total = merged.reduce((s, d) => s + d.quantity, 0);
  const filledCount = merged.filter((d) => d.quantity > 0).length;

  if (total === 0) {
    return (
      <Paper
        withBorder
        radius="md"
        p="lg"
        className="border-dashed bg-gray-50 text-center"
      >
        <Text c="gray.6" size="sm">
          No resources recorded yet.
        </Text>
      </Paper>
    );
  }

  const categories = merged.map((d) => d.label);
  const data = merged.map((d) => d.quantity);
  const colors = merged.map((d) =>
    d.quantity > 0 ? ITEM_TYPE_COLORS[d.itemType] : EMPTY_COLOR,
  );

  return (
    <Stack gap="xs">
      <Group justify="space-between" align="flex-end" wrap="nowrap">
        <Group gap="xs" align="baseline" wrap="nowrap">
          <Text fw={700} size="xl" className="tabular-nums">
            {total.toLocaleString()}
          </Text>
          <Text size="sm" c="gray.6">
            units pledged
          </Text>
        </Group>
        <Badge variant="light" color="blue" size="sm">
          {filledCount} of {ITEM_TYPES.length} categories
        </Badge>
      </Group>

      <ApexChart
        type="bar"
        height={height}
        series={[{ name: "Quantity", data }]}
        options={{
          chart: {
            toolbar: { show: false },
            fontFamily: "inherit",
            animations: { enabled: true, speed: 350 },
            background: "transparent",
          },
          plotOptions: {
            bar: {
              borderRadius: 8,
              borderRadiusApplication: "end",
              columnWidth: "50%",
              distributed: true,
              dataLabels: { position: "top" },
            },
          },
          dataLabels: {
            enabled: true,
            offsetY: -20,
            formatter: (v: number) => (v > 0 ? v.toLocaleString() : ""),
            style: { fontSize: "11px", fontWeight: 600, colors: ["#334155"] },
          },
          colors,
          fill: {
            type: "gradient",
            gradient: {
              shade: "light",
              type: "vertical",
              shadeIntensity: 0.15,
              gradientToColors: undefined,
              inverseColors: false,
              opacityFrom: 1,
              opacityTo: 0.75,
              stops: [0, 100],
            },
          },
          stroke: { show: false },
          xaxis: {
            categories,
            labels: {
              style: { fontSize: "12px", colors: "#475569", fontWeight: 500 },
            },
            axisTicks: { show: false },
            axisBorder: { show: false },
          },
          yaxis: {
            labels: {
              style: { fontSize: "11px", colors: "#94a3b8" },
              formatter: (v: number) =>
                v >= 1000 ? `${(v / 1000).toFixed(1)}k` : `${v}`,
            },
          },
          grid: {
            borderColor: "#eef2f7",
            strokeDashArray: 4,
            padding: { top: 10, right: 10, bottom: 0, left: 10 },
            yaxis: { lines: { show: true } },
            xaxis: { lines: { show: false } },
          },
          tooltip: {
            theme: "light",
            y: {
              formatter: (v: number) =>
                v > 0 ? `${v.toLocaleString()} units` : "No pledges yet",
            },
          },
          states: {
            hover: { filter: { type: "darken" } },
            active: { filter: { type: "none" } },
          },
          legend: { show: false },
        }}
      />
    </Stack>
  );
}
