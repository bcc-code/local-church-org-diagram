<template>
    <div @click="handleNodeClick"
        :class="[
            'box-border w-full h-full p-3 rounded-lg shadow-sm flex items-center hover:bg-neutral-50 transition-colors cursor-pointer',
            props.isExpanded
                ? 'border-2 border-brand-500 bg-brand-50'
                : 'border border-neutral-200 bg-neutral-0'
        ]">
        <div class="ml-3 min-w-0 flex-1">
            <div class="text-body-sm font-semibold text-neutral-900 whitespace-nowrap overflow-hidden text-ellipsis"
                :title="name">
                {{ name }}
            </div>
            <div class="text-caption text-neutral-600 whitespace-nowrap overflow-hidden text-ellipsis" :title="title">
                {{ title }}
            </div>
        </div>
        <div v-if="props.staffGroups && props.staffGroups.length > 0" class="flex-shrink-0 ml-2">
            <div class="w-5 h-5 rounded-full bg-brand-500 text-neutral-0 flex items-center justify-center text-xs font-bold"
                :title="`${props.staffGroups.length} staber`">
                {{ props.staffGroups.length }}
            </div>
        </div>
    </div>

    <GroupMembersDialog v-if="props.groupId !== undefined && props.groupId !== null" ref="membersDialog"
        :group-id="props.groupId" :group-name="name" :admin-mode="props.adminMode" />
    <StaffGroupsDialog v-if="props.staffGroups && props.staffGroups.length > 0" ref="staffDialog" :group-name="name"
        :staff-groups="props.staffGroups" />
</template>

<script lang="ts" setup>
import { ref } from 'vue';
import GroupMembersDialog from './GroupMembersDialog.vue';
import StaffGroupsDialog from './StaffGroupsDialog.vue';
import type { Group } from '@/types';

interface Props {
    name: string;
    title?: string;
    groupId?: number | string;
    memberCount: number;
    width?: number;
    height?: number;
    depth?: number;
    parentGroupId?: number | string | null;
    raw?: any;
    staffGroups?: Group[];
    adminMode?: boolean;
    isExpanded?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
    adminMode: false,
    isExpanded: false,
});

const membersDialog = ref<InstanceType<typeof GroupMembersDialog> | null>(null);
const staffDialog = ref<InstanceType<typeof StaffGroupsDialog> | null>(null);

const handleNodeClick = () => {
    // Priority: if has staff groups, show those first; otherwise show members dialog
    if (props.staffGroups && props.staffGroups.length > 0 && staffDialog.value) {
        staffDialog.value.open();
    } else if (membersDialog.value) {
        membersDialog.value.open();
    }
};
</script>
