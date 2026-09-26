// Text constants (for potential i18n in the future)
export const TEXTS = {
  APP_TITLE: "Organisasjonskart",
  MEMBERS: "medlemmer",
  STAFF_GROUPS: "staber",
  LOADING_MEMBERS: "Laster medlemmer...",
  NO_MEMBERS: "Ingen medlemmer i denne gruppen.",
  NO_MEMBERS_SHORT: "",
  COULD_NOT_LOAD_MEMBERS: "Kunne ikke laste medlemmer.",
  ERROR_LOADING_DATA: "Failed to load org data",
  ERROR_D3_NOT_LOADED: "d3-org-chart not loaded",
} as const;

// UI Configuration
export const UI_CONFIG = {
  CHART: {
    CHILDREN_MARGIN: 100,
    NEIGHBOUR_MARGIN: 50,
    SIBLINGS_MARGIN: 20,
    SIBLINGS_MARGIN_ADMIN: 40,
    NODE_HEIGHT: 100,
    NODE_WIDTH: 200,
    COMPACT: false,
  },
  DIALOG: {
    MAX_HEIGHT: "max-h-lg",
    MAX_WIDTH: "max-w-lg",
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
    TITLES: "/titles",
    GROUP_SORT_ORDER: "/groups/sort-order",
  },
} as const;

// Auth endpoints (served outside the /api prefix, see backend/auth.py)
export const AUTH_CONFIG = {
  ENDPOINTS: {
    USER: "/user",
    LOGIN: "/login",
    LOGOUT: "/logout",
  },
} as const;

// Role ids, matching the `role` table ids (see backend/demo_requests/roles.json)
export const ROLES = {
  GLOBAL_ADMIN: 1,
  LOCAL_ADMIN: 2,
  GROUP_ADMIN: 3,
} as const;
