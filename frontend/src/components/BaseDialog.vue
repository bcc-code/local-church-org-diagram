<template>
    <Dialog v-model:open="isOpen">
        <DialogContent :class="`${UI_CONFIG.DIALOG.MAX_WIDTH} bg-neutral-0 border-neutral-200`">
            <DialogClose as-child class="absolute right-3 top-2">
                <Button size="sm" variant="destructive" class="opacity-80 hover:opacity-100">
                    ×
                </Button>
            </DialogClose>
            <DialogHeader>
                <DialogTitle class="text-heading-lg text-neutral-900">{{ title }}</DialogTitle>
                <DialogDescription class="text-body-sm text-neutral-600">
                    {{ description }}
                </DialogDescription>
            </DialogHeader>

            <div :class="`${UI_CONFIG.DIALOG.MAX_HEIGHT} overflow-y-auto space-y-2 py-2`">
                <slot />
            </div>
        </DialogContent>
    </Dialog>
</template>

<script lang="ts" setup>
import { ref } from 'vue';
import Button from '@/components/ui/button/Button.vue';
import Dialog from '@/components/ui/dialog/Dialog.vue';
import DialogContent from '@/components/ui/dialog/DialogContent.vue';
import DialogHeader from '@/components/ui/dialog/DialogHeader.vue';
import DialogTitle from '@/components/ui/dialog/DialogTitle.vue';
import DialogDescription from '@/components/ui/dialog/DialogDescription.vue';
import DialogClose from '@/components/ui/dialog/DialogClose.vue';
import { UI_CONFIG } from '@/constants';
import type { DialogProps } from '@/types';

defineProps<DialogProps>();

const isOpen = ref(false);

const open = () => {
    isOpen.value = true;
};

const close = () => {
    isOpen.value = false;
};

defineExpose({
    open,
    close
});
</script>