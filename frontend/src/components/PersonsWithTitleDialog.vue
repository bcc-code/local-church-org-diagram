<template>
  <BaseDialog
    ref="baseDialog"
    :title="title"
    :description="dialogDescription"
  >
    <div
      v-if="hasError()"
      class="p-4 bg-red-50 border border-red-200 rounded-md"
    >
      <p class="text-red-800 text-sm">
        {{ state.error }}
      </p>
    </div>
    <div
      v-else-if="!isLoading() && hasData()"
      class="space-y-2 overflow-y-auto max-h-[60vh]"
    >
      <button
        v-for="entry in state.data"
        :key="entry.person_uid"
        class="w-full flex items-center justify-between p-3 border border-silver-tree-400 rounded-lg bg-neutral-50 hover:bg-brand-50 hover:border-brand-500 transition-colors cursor-pointer"
        @click="handleEntryClick(entry)"
      >
        <div class="flex-1 text-left">
          <div class="text-body-md font-medium text-neutral-900">
            {{ entry.name }}
          </div>
        </div>
        <svg
          class="w-5 h-5 text-brand-600"
          fill="none"
          stroke="currentColor"
          viewBox="0 0 24 24"
        >
          <path
            stroke-linecap="round"
            stroke-linejoin="round"
            stroke-width="2"
            d="M9 5l7 7-7 7"
          />
        </svg>
      </button>
    </div>
    <div
      v-else-if="!isLoading() && !hasData()"
      class="p-4 text-center text-neutral-600"
    >
      <p>Ingen medlemmer med denne tittelen</p>
    </div>
  </BaseDialog>
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue';
import BaseDialog from './BaseDialog.vue';
import { useAsyncData, useApiClient } from '@/composables/useApi';
import type { PersonWithTitle } from '@/types';

const { state, execute, isLoading, hasError, hasData } = useAsyncData<PersonWithTitle[]>();
const { fetchPersonsWithTitle } = useApiClient();
const baseDialog = ref<InstanceType<typeof BaseDialog> | null>(null);
const title = ref('');

const dialogDescription = computed(() => {
    if (isLoading()) return 'Laster medlemmer...';
    if (hasError()) return 'Kunne ikke laste medlemmer';
    if (hasData() && state.value.data) {
        const count = state.value.data.length;
        return `${count} ${count === 1 ? 'medlem' : 'medlemmer'} med denne tittelen:`;
    }
    return 'Ingen medlemmer';
});

const emit = defineEmits<{
    'person-selected': [person: PersonWithTitle]
}>();

const open = async (searchedTitle: string) => {
    title.value = searchedTitle;
    if (baseDialog.value) {
        baseDialog.value.open();
        await execute(() => fetchPersonsWithTitle(searchedTitle));
    }
};

const handleEntryClick = (entry: PersonWithTitle) => {
    emit('person-selected', entry);
    if (baseDialog.value) {
        baseDialog.value.close();
    }
};

defineExpose({
    open
});
</script>
