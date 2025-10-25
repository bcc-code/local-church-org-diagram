// Text constants (for potential i18n in the future)
export const TEXTS = {
  APP_TITLE: "Organisasjonskart",
  MEMBERS: "medlemmer",
  STAFF_GROUPS: "staber",
  LOADING_MEMBERS: "Laster medlemmer...",
  NO_MEMBERS: "Ingen medlemmer i denne gruppen.",
  COULD_NOT_LOAD_MEMBERS: "Kunne ikke laste medlemmer.",
  ERROR_LOADING_DATA: "Failed to load org data",
  ERROR_D3_NOT_LOADED: "d3-org-chart not loaded",
} as const;

// UI Configuration
export const UI_CONFIG = {
  CHART: {
    CHILDREN_MARGIN: 100,
    NEIGHBOUR_MARGIN: 50,
    NODE_HEIGHT: 100,
    NODE_WIDTH: 200,
    COMPACT: false,
  },
  DIALOG: {
    MAX_HEIGHT: "max-h-64",
    MAX_WIDTH: "max-w-md",
  },
  TRANSITIONS: {
    DURATION: "150ms",
  },
} as const;

// API Configuration
export const API_CONFIG = {
  BASE_URL: "/api",
  ENDPOINTS: {
    TREE: "/tree",
    PERSONS: "/persons",
  },
} as const;
