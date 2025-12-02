<template>
    <BaseDialog ref="baseDialog" :title="`${personName}`" :description="dialogDescription">
        <div v-if="hasError()" class="p-4 bg-red-50 border border-red-200 rounded-md">
            <p class="text-red-800 text-sm">{{ state.error }}</p>
        </div>
        <div v-else-if="!isLoading() && hasData()" class="space-y-2 overflow-y-auto max-h-[60vh]">
            <button v-for="group in state.data" :key="group.group_id" @click="handleGroupClick(group)"
                class="w-full flex items-center justify-between p-3 border border-silver-tree-400 rounded-lg bg-neutral-50 hover:bg-brand-50 hover:border-brand-500 transition-colors cursor-pointer">
                <div class="flex-1 text-left">
                    <div class="text-body-md font-medium text-neutral-900">
                        {{ group.name }}
                    </div>
                    <div class="text-sm text-neutral-500">
                        {{ group.member_count }} {{ group.member_count === 1 ? 'medlem' : 'medlemmer' }}
                    </div>
                </div>
                <svg class="w-5 h-5 text-brand-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
                </svg>
            </button>
        </div>
        <div v-else-if="!isLoading() && !hasData()" class="p-4 text-center text-neutral-600">
            <p>Ingen grupper funnet</p>
        </div>
    </BaseDialog>
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue';
import BaseDialog from './BaseDialog.vue';
import { useAsyncData, useApiClient } from '@/composables/useApi';

interface GroupInfo {
    group_id: number;
    name: string;
    member_count: number;
}

interface Props {
    personName: string;
}

const props = defineProps<Props>();

const emit = defineEmits<{
    'group-selected': [groupId: number]
}>();

const { state, execute, isLoading, hasError, hasData } = useAsyncData<GroupInfo[]>();
const { getPersonGroups } = useApiClient();
const baseDialog = ref<InstanceType<typeof BaseDialog> | null>(null);

const dialogDescription = computed(() => {
    if (isLoading()) return 'Laster grupper...';
    if (hasError()) return 'Kunne ikke laste grupper';
    if (hasData() && state.value.data) {
        const count = state.value.data.length;
        return `Medlem av ${count} ${count === 1 ? 'gruppe' : 'grupper'}:`;
    }
    return 'Ingen grupper';
});

const open = async (personUid: string) => {
    if (baseDialog.value) {
        baseDialog.value.open();
        await execute(async () => {
            const response = await getPersonGroups(personUid);
            return response.groups || [];
        });
    }
};

const handleGroupClick = (group: GroupInfo) => {
    emit('group-selected', group.group_id);
    if (baseDialog.value) {
        baseDialog.value.close();
    }
};

defineExpose({
    open
});
</script>
