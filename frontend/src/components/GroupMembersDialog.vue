<template>
    <BaseDialog ref="baseDialog" :title="`${groupName} - Medlemmer`" :description="dialogDescription">
        <div v-if="hasError()" class="p-4 bg-red-50 border border-red-200 rounded-md">
            <p class="text-red-800 text-sm">{{ state.error }}</p>
        </div>
        <div v-else-if="!isLoading() && hasData()" class="space-y-3">
            <MemberCard v-for="member in state.data" :key="member.person_uid" :member="member" />
        </div>
        <div v-else-if="!isLoading() && !hasData()" class="p-4 text-center text-neutral-600">
            <p>{{ TEXTS.NO_MEMBERS }}</p>
        </div>
    </BaseDialog>
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue';
import BaseDialog from './BaseDialog.vue';
import MemberCard from './MemberCard.vue';
import { useAsyncData, useApiClient } from '@/composables/useApi';
import { TEXTS } from '@/constants';
import type { GroupMember } from '@/types';

interface Props {
    groupId?: number | string;
    memberCount?: number;
    groupName: string;
}

const props = defineProps<Props>();

const { state, execute, isLoading, hasError, hasData } = useAsyncData<GroupMember[]>();
const { fetchGroupMembers } = useApiClient();
const baseDialog = ref<InstanceType<typeof BaseDialog> | null>(null);

const dialogDescription = computed(() => {
    if (isLoading()) return TEXTS.LOADING_MEMBERS;
    if (hasError()) return TEXTS.COULD_NOT_LOAD_MEMBERS;
    if (hasData() && state.value.data) return `${state.value.data.length} ${TEXTS.MEMBERS} i gruppen:`;
    return TEXTS.NO_MEMBERS;
});

const open = () => {
    if (baseDialog.value) {
        baseDialog.value.open();
        if (!hasData() && props.groupId) {
            loadGroupMembers();
        }
    }
};

const loadGroupMembers = async () => {
    if (!props.groupId) return;

    await execute(() => fetchGroupMembers(props.groupId!));
};

defineExpose({
    open
});
</script>