<template>

    <div v-if="state.loading"
        class="rounded-xl bg-neutral-0 min-h-full shadow-lg border border-neutral-200 flex grow items-center justify-center">
        <div class="text-center">
            <div class="w-8 h-8 border-4 border-brand-200 border-t-brand-600 rounded-full animate-spin mx-auto mb-4">
            </div>
            <p class="text-neutral-600">Laster organisasjonskart...</p>
        </div>
    </div>
    <div v-else-if="state.error"
        class="rounded-xl grow min-h-full bg-neutral-0 shadow-lg border border-neutral-200 flex items-center justify-center">
        <div class="text-center p-6">
            <div class="w-12 h-12 bg-red-100 rounded-full flex items-center justify-center mx-auto mb-4">
                <svg class="w-6 h-6 text-red-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                        d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L3.082 16.5c-.77.833.192 2.5 1.732 2.5z" />
                </svg>
            </div>
            <p class="text-red-800 font-medium">Kunne ikke laste organisasjonskart</p>
            <p class="text-red-600 text-sm mt-1">{{ state.error }}</p>
        </div>
    </div>
    <div v-else-if="state.data" ref="chartEl"
        class="rounded-xl flex min-h-full grow bg-neutral-0 shadow-lg border border-neutral-200" />
</template>

<script lang="ts" setup>
import { onMounted, ref, createApp, nextTick, watch } from 'vue';
import OrgNode from './OrgNode.vue';
import { useAsyncData, useApiClient } from '@/composables/useApi';
import { TEXTS, UI_CONFIG } from '@/constants';
import type { Group, OrgNodeData } from '@/types';

interface Props {
  adminMode?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  adminMode: false,
});

const chartEl = ref<HTMLDivElement | null>(null);
const { state, execute } = useAsyncData<Group[]>();
const { fetchGroups } = useApiClient();

// Helper function to wait for D3 to be loaded
const waitForD3 = (): Promise<void> => {
    return new Promise((resolve) => {
        const checkD3 = () => {
            const d3 = (window as any).d3;
            if (d3 && d3.OrgChart) {
                resolve();
            } else {
                setTimeout(checkD3, 100);
            }
        };
        checkD3();
    });
};

const toOrgNodes = (groups: Group[]): OrgNodeData[] => {
    // Separate staff groups from regular groups
    const staffGroups = groups.filter(g => g.type === 'staff-group');
    const regularGroups = groups.filter(g => g.type !== 'staff-group');

    // Create a map of parent_group_id -> staff groups
    const staffGroupsByParent = new Map<number, Group[]>();
    staffGroups.forEach(sg => {
        if (sg.parent_group_id) {
            if (!staffGroupsByParent.has(sg.parent_group_id)) {
                staffGroupsByParent.set(sg.parent_group_id, []);
            }
            staffGroupsByParent.get(sg.parent_group_id)!.push(sg);
        }
    });

    return regularGroups.map((g) => {
        const title = g.member_count > 0
            ? `${g.member_count} ${TEXTS.MEMBERS}`
            : TEXTS.NO_MEMBERS_SHORT;
        const staffGroups = staffGroupsByParent.get(g.group_id) || [];

        return {
            id: g.group_id,
            parentId: g.parent_group_id,
            name: g.label,
            title,
            raw: g,
            staffGroups,
        };
    });
};

onMounted(async () => {
    // Wait for D3 library to be loaded
    await waitForD3();

    await execute(fetchGroups);
});

// Watch for when data is loaded and render chart
watch(() => state.value.data, async (newData) => {
    if (newData && !state.value.loading && !state.value.error) {
        // Wait for the DOM to update (chartEl to be available)
        await nextTick();
        const data: OrgNodeData[] = toOrgNodes(newData);
        renderChart(data);
    }
}, { immediate: false });

const renderChart = (data: OrgNodeData[]) => {
    // Ensure the DOM element is available
    if (!chartEl.value) {
        console.error('Chart container element not available');
        return;
    }

    const d3 = (window as any).d3;
    if (!d3 || !d3.OrgChart) {
        console.error(TEXTS.ERROR_D3_NOT_LOADED);
        return;
    }

    try {
        const chart: any = new d3.OrgChart()
            .container(chartEl.value as HTMLElement)
            .data(data)
            .nodeId((d: OrgNodeData) => d.id)
            .parentNodeId((d: OrgNodeData) => d.parentId)
            .childrenMargin(() => UI_CONFIG.CHART.CHILDREN_MARGIN)
            .neighbourMargin(() => UI_CONFIG.CHART.NEIGHBOUR_MARGIN)
            .nodeHeight(() => UI_CONFIG.CHART.NODE_HEIGHT)
            .nodeWidth(() => UI_CONFIG.CHART.NODE_WIDTH)
            .compact(UI_CONFIG.CHART.COMPACT)
            .nodeContent((node: any, _i: number, _arr: any[], state: any) => {
                const w = state.nodeWidth(node);
                const h = state.nodeHeight(node);
                const id = state.nodeId(node.data);
                return `<div class="org-node-mount" data-node-id="${id}" style=";min-width:${w}px;height:${h}px;"></div>`;
            })
            .render();

        // Mount Vue components into each node placeholder
        const mountedApps: Record<string, ReturnType<typeof createApp>> = {};

        const mountNodes = () => {
            const container: HTMLElement | null = chartEl.value;
            if (!container) return;
            const state = chart.getChartState();
            const mounts = container.querySelectorAll('.org-node-mount');

            mounts.forEach((el) => {
                const host = el as HTMLElement;
                const nodeId = host.getAttribute('data-node-id') || '';
                const node = (state.allNodes || []).find(
                    (n: any) => state.nodeId(n.data).toString() === nodeId.toString(),
                );
                if (!node) return;

                const data: OrgNodeData = node.data;
                const width = state.nodeWidth(node);
                const height = state.nodeHeight(node);
                const isExpanded = node.children && node.children.length > 0;

                // Avoid leaks on updates
                mountedApps[nodeId]?.unmount?.();
                host.innerHTML = '';

                const app = createApp(OrgNode, {
                    name: data.name,
                    title: data.title,
                    width,
                    height,
                    groupId: data.id,
                    memberCount: data.raw.member_count,
                    parentGroupId: data.parentId,
                    raw: data.raw,
                    staffGroups: data.staffGroups || [],
                    adminMode: props.adminMode,
                    isExpanded,
                });
                app.mount(host);
                mountedApps[nodeId] = app;
            });
        };

        mountNodes();

        // Re-mount after any chart restyle/update
        const origRestyle = chart.restyleForeignObjectElements.bind(chart);
        chart.restyleForeignObjectElements = (...args: any[]) => {
            const res = origRestyle(...args);
            mountNodes();
            return res;
        };

        // Expose for debugging
        (window as any).orgChart = chart;
    } catch (error) {
        console.error('Failed to render org chart:', error);
        // The error will be caught by the async handler in onMounted
        throw error;
    }
};
</script>