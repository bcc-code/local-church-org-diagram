<template>
    <div class="box-border w-full h-full p-3 border border-[#E4E2E9] rounded-lg bg-white shadow-sm flex items-center"
        @click="test">
        <div
            class=" w-11 h-11 rounded-full bg-[#4c6ef5] text-white flex items-center justify-center font-bold flex-shrink-0">
            {{ computedInitials }}
        </div>
        <div class="ml-3 min-w-0">
            <div class="text-sm font-bold text-[#111] whitespace-nowrap overflow-hidden text-ellipsis" :title="name">
                {{ name }}
            </div>
            <div class="text-xs text-[#666] whitespace-nowrap overflow-hidden text-ellipsis" :title="title">
                {{ title }}
            </div>
        </div>
        <Dialog>
            <DialogTrigger as-child>
                <div class="flex ml-auto">
                    <Button variant="outline" size="icon">
                        +
                    </Button>
                </div>
            </DialogTrigger>
            <DialogContent>
                <DialogHeader>
                    <DialogTitle>Invite user</DialogTitle>
                    <DialogDescription>Send an invitation link to this user.

                        <div v-for="peopleId in peopleIds22" :key="peopleId" class="text-sm text-gray-500 mt-2">
                            Person ID: {{ peopleId }}
                        </div>
                    </DialogDescription>

                </DialogHeader>
                <DialogFooter>
                    <DialogClose as-child>
                        <Button variant="secondary">Close</Button>
                    </DialogClose>
                    <Button>Send</Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    </div>
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue';

interface Props {
    name: string;
    title?: string;
    groupId?: number;
    peopleIds?: string[];
    width?: number;
    height?: number;
}

const props = defineProps<Props>();

import Button from '@/components/ui/button/Button.vue';
import Dialog from '@/components/ui/dialog/Dialog.vue';
import DialogTrigger from '@/components/ui/dialog/DialogTrigger.vue';
import DialogContent from '@/components/ui/dialog/DialogContent.vue';
import DialogHeader from '@/components/ui/dialog/DialogHeader.vue';
import DialogTitle from '@/components/ui/dialog/DialogTitle.vue';
import DialogDescription from '@/components/ui/dialog/DialogDescription.vue';
import DialogFooter from '@/components/ui/dialog/DialogFooter.vue';
import DialogClose from '@/components/ui/dialog/DialogClose.vue';

const test = () => {
    console.log('clicked');
};


const peopleIds22 = ref<{
    name: string;
    person_uid: string;
}[]>([]);



const fetchPeopleIds = async () => {
    if (!props.groupId) return;
    try {
        const res = await fetch('/persons.json');
        const people: { name: string; person_uid: string; group_id: number }[] = await res.json();
        console.log('Fetched people:', people);

        // Filter people based on props.peopleIds if provided
        if (props.peopleIds && props.peopleIds.length > 0) {
            peopleIds22.value = people.filter(person =>
                props.peopleIds!.includes(person.person_uid)
            );
        } else {
            peopleIds22.value = people;
        }
    } catch (error) {
        console.error('Failed to fetch people IDs', error);
    }
};
fetchPeopleIds();

const computedInitials = computed(() => {
    const n = props.name || '';
    return n
        .split(' ')
        .filter(Boolean)
        .slice(0, 2)
        .map((s) => s[0]?.toUpperCase())
        .join('');
});
</script>
