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
                <Icon name="TriangleAlert" :size="24" color="rgb(220 38 38)" />
            </div>
            <p class="text-red-800 font-medium">Kunne ikke laste organisasjonskart</p>
            <p class="text-red-600 text-sm mt-1">{{ state.error }}</p>
        </div>
    </div>
    <div v-else-if="state.data"
        class="relative rounded-xl flex min-h-full grow bg-neutral-0 shadow-lg border border-neutral-200">
        <div ref="chartEl" class="w-full h-full" />

        <!-- Person Groups Dialog -->
        <PersonGroupsDialog ref="personGroupsDialog" :person-name="selectedPerson?.name || ''"
            @group-selected="handleGroupSelected" />

        <!-- Unified search input -->
        <div class="absolute top-3 left-3 w-48">
            <div class="relative">
                <input v-model="unifiedSearchQuery" @input="handleUnifiedSearch" type="text"
                    placeholder="person: eller gruppe:"
                    class="w-full px-2 py-1.5 text-sm border-2 border-brand-500 bg-brand-50 rounded-lg focus:outline-none focus:bg-white placeholder:text-neutral-400" />

                <!-- Group results -->
                <div v-if="searchResults.length > 0"
                    class="absolute z-50 w-full mt-1 bg-white border-2 border-brand-500 rounded-lg shadow-sm overflow-hidden max-h-48 overflow-y-auto">
                    <button v-for="result in searchResults" :key="result.id" @click="navigateToNode(result)"
                        class="w-full px-2 py-1.5 text-left text-sm hover:bg-brand-50 focus:bg-brand-50 focus:outline-none border-b border-neutral-100 last:border-b-0">
                        {{ result.name }}
                    </button>
                </div>

                <!-- Person results -->
                <div v-if="personSearchResults.length > 0"
                    class="absolute z-50 w-full mt-1 bg-white border-2 border-brand-500 rounded-lg shadow-sm overflow-hidden max-h-48 overflow-y-auto">
                    <button v-for="person in personSearchResults" :key="person.person_uid"
                        @click="findPersonGroups(person)"
                        class="w-full px-2 py-1.5 text-left text-sm hover:bg-brand-50 focus:bg-brand-50 focus:outline-none border-b border-neutral-100 last:border-b-0">
                        {{ person.name }}
                    </button>
                </div>
            </div>
        </div>

        <!-- Control buttons -->
        <div class="absolute top-3 right-3 flex gap-2">
            <button @click="centerChart"
                class="w-8 h-8 flex items-center justify-center rounded-lg border-2 border-brand-500 bg-brand-50 hover:bg-brand-100 shadow-sm transition-colors"
                title="Center chart">
                <Icon name="Maximize" :size="16" class="text-brand-600" />
            </button>
            <button @click="expandAll"
                class="w-8 h-8 flex items-center justify-center rounded-lg border-2 border-brand-500 bg-brand-50 hover:bg-brand-100 shadow-sm transition-colors"
                title="Expand all">
                <Icon name="Plus" :size="16" class="text-brand-600" />
            </button>
            <button @click="collapseAll"
                class="w-8 h-8 flex items-center justify-center rounded-lg border-2 border-brand-500 bg-brand-50 hover:bg-brand-100 shadow-sm transition-colors"
                title="Collapse all">
                <Icon name="Minus" :size="16" class="text-brand-600" />
            </button>
        </div>
    </div>
</template>

<script lang="ts" setup>
import { onMounted, ref, createApp, nextTick, watch } from 'vue';
import OrgNode from './OrgNode.vue';
import Icon from './ui/icon/Icon.vue';
import PersonGroupsDialog from './PersonGroupsDialog.vue';
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
const personGroupsDialog = ref<InstanceType<typeof PersonGroupsDialog> | null>(null);
const { state, execute } = useAsyncData<Group[]>();
const { fetchGroups, searchPersons } = useApiClient();
let chart: any = null;
let skipNextRender = false;

// Unified search state
const unifiedSearchQuery = ref('');
const searchResults = ref<OrgNodeData[]>([]);
const personSearchResults = ref<any[]>([]);
let allNodes: OrgNodeData[] = [];
let personSearchTimeout: ReturnType<typeof setTimeout> | null = null;
let selectedPerson: any = null;

// Chart control functions
const centerChart = () => {
    if (chart) {
        chart.fit();
    }
};

const expandAll = () => {
    if (chart) {
        chart.expandAll();
    }
};

const collapseAll = () => {
    if (chart) {
        chart.collapseAll();
    }
};

// Unified search functionality
const handleUnifiedSearch = () => {
    const fullQuery = unifiedSearchQuery.value.trim();

    // Clear previous timeout
    if (personSearchTimeout) {
        clearTimeout(personSearchTimeout);
    }

    // Reset results
    searchResults.value = [];
    personSearchResults.value = [];

    // Check for person: prefix
    if (fullQuery.toLowerCase().startsWith('person:')) {
        const query = fullQuery.substring(7).trim(); // Remove 'person:' prefix

        if (query.length < 3) {
            return;
        }

        // Debounce API call for person search
        personSearchTimeout = setTimeout(async () => {
            try {
                const results = await searchPersons(query);
                personSearchResults.value = results;
            } catch (error) {
                console.error('Person search failed:', error);
                personSearchResults.value = [];
            }
        }, 300);
    }
    // Check for gruppe: prefix
    else if (fullQuery.toLowerCase().startsWith('gruppe:')) {
        const query = fullQuery.substring(7).trim().toLowerCase(); // Remove 'gruppe:' prefix

        if (query.length < 2) {
            return;
        }

        searchResults.value = allNodes.filter(node =>
            node.name.toLowerCase().includes(query)
        ).slice(0, 10); // Limit to 10 results
    }
};

const navigateToNode = (node: OrgNodeData) => {
    if (!chart) return;

    // Clear search
    unifiedSearchQuery.value = '';
    searchResults.value = [];

    // Find the path from root to this node
    const path: (number | string)[] = [];
    let currentNode = node;

    while (currentNode) {
        path.unshift(currentNode.id);
        const parentNode = allNodes.find(n => n.id === currentNode.parentId);
        if (!parentNode) break;
        currentNode = parentNode;
    }

    // Expand all nodes in the path
    path.forEach(nodeId => {
        chart.setExpanded(nodeId);
    });

    // Center on the target node
    chart.setCentered(node.id).render();
};

const findPersonGroups = (person: any) => {
    // Clear search
    unifiedSearchQuery.value = '';
    personSearchResults.value = [];

    // Store selected person and open dialog
    selectedPerson = person;
    if (personGroupsDialog.value) {
        personGroupsDialog.value.open(person.person_uid);
    }
};

const handleGroupSelected = (groupId: number) => {
    if (!chart) return;

    // Find the node
    const node = allNodes.find(n => n.id === groupId);
    if (!node) {
        console.error('Group not found:', groupId);
        return;
    }

    // Navigate to the node
    navigateToNode(node);
};

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

// Handler for member count updates
const handleMemberCountChanged = (groupId: number | string, newCount: number) => {
    if (!state.value.data) return;

    // Update the group in state.data
    const groupIndex = state.value.data.findIndex(g => g.group_id === groupId);
    if (groupIndex !== -1) {
        // Set flag to skip the next watcher trigger
        skipNextRender = true;

        // Create a new array to trigger reactivity (but watcher will skip it)
        const updatedData = [...state.value.data];
        updatedData[groupIndex] = {
            ...updatedData[groupIndex],
            member_count: newCount
        };
        state.value.data = updatedData;

        // Update only the title text in the DOM without re-rendering the chart
        // This prevents the dialog from closing
        if (chartEl.value) {
            const nodeElement = chartEl.value.querySelector(`[data-node-id="${groupId}"]`);
            if (nodeElement) {
                const titleElement = nodeElement.querySelector('.text-caption');
                if (titleElement) {
                    const newTitle = newCount > 0
                        ? `${newCount} ${TEXTS.MEMBERS}`
                        : TEXTS.NO_MEMBERS_SHORT;
                    titleElement.textContent = newTitle;
                }
            }
        }
    }
};

onMounted(async () => {
    // Wait for D3 library to be loaded
    await waitForD3();

    await execute(fetchGroups);
});

// Watch for when data is loaded and render chart
watch(() => state.value.data, async (newData) => {
    if (skipNextRender) {
        skipNextRender = false;
        return;
    }
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

    // Store nodes for search
    allNodes = data;

    try {
        chart = new d3.OrgChart()
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
                    onMemberCountChanged: handleMemberCountChanged,
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
    } catch (error) {
        console.error('Failed to render org chart:', error);
        // The error will be caught by the async handler in onMounted
        throw error;
    }
};
</script>