<script lang="ts" setup>
import { Primitive } from 'radix-vue';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';

const buttonVariants = cva(
    'inline-flex items-center justify-center whitespace-nowrap rounded-md text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 disabled:pointer-events-none disabled:opacity-50',
    {
        variants: {
            variant: {
                default: 'bg-brand-600 text-neutral-0 hover:bg-brand-700',
                secondary: 'bg-neutral-100 text-neutral-900 hover:bg-neutral-200',
                outline: 'border border-neutral-300 bg-neutral-0 hover:bg-neutral-50 hover:text-neutral-900',
                ghost: 'hover:bg-neutral-100 hover:text-neutral-900',
                link: 'text-brand-600 underline-offset-4 hover:underline',
                destructive: 'bg-red-600 text-neutral-0 hover:bg-red-700',
            },
            size: {
                default: 'h-9 px-4 py-2',
                sm: 'h-8 rounded-md px-3 text-xs',
                lg: 'h-10 rounded-md px-8',
                icon: 'h-9 w-9',
            },
        },
        defaultVariants: {
            variant: 'default',
            size: 'default',
        },
    }
);

type ButtonVariants = VariantProps<typeof buttonVariants>;
const props = withDefaults(
    defineProps<{
        as?: string;
        class?: string;
        variant?: ButtonVariants['variant'];
        size?: ButtonVariants['size'];
    }>(),
    {}
);
</script>

<template>
    <Primitive :as="props.as ?? 'button'"
        :class="cn(buttonVariants({ variant: props.variant, size: props.size }), props.class)">
        <slot />
    </Primitive>

</template>
