<template>
    <div @click="handleNodeClick"
        class="box-border w-full h-full p-3 border border-neutral-200 rounded-lg bg-neutral-0 shadow-sm flex items-center hover:bg-neutral-50 transition-colors cursor-pointer">
        <div
            class="w-11 h-11 rounded-full bg-silver-tree-600 text-neutral-0 flex items-center justify-center font-bold flex-shrink-0">
            {{ computedInitials }}
        </div>
        <div class="ml-3 min-w-0">
            <div class="text-body-sm font-semibold text-neutral-900 whitespace-nowrap overflow-hidden text-ellipsis"
                :title="name">
                {{ name }}
            </div>
            <div class="text-caption text-neutral-600 whitespace-nowrap overflow-hidden text-ellipsis" :title="title">
                {{ title }}
            </div>
        </div>
    </div>

    <GroupMembersDialog v-if="props.peopleCount > 0" ref="membersDialog" :group-id="groupId" :people-ids="peopleIds"
        :group-name="name" />
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue';
import GroupMembersDialog from './GroupMembersDialog.vue';

interface Props {
    name: string;
    title?: string;
    groupId?: number | string;
    peopleIds?: string[];
    width?: number;
    height?: number;
    depth?: number;
    parentGroupId?: number | string | null;
    peopleCount: number;
    raw?: any;
}

const props = defineProps<Props>();

const membersDialog = ref<InstanceType<typeof GroupMembersDialog> | null>(null);

const computedInitials = computed(() => {
    const n = props.name || '';
    return n
        .split(' ')
        .filter(Boolean)
        .slice(0, 2)
        .map((s) => s[0]?.toUpperCase())
        .join('');
});

const handleNodeClick = () => {
    if (props.peopleCount > 0 && membersDialog.value) {
        membersDialog.value.open();
    }
};
</script>