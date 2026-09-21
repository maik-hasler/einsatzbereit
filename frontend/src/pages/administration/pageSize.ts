/**
 * How many rows each administration list loads at a time.
 *
 * One value rather than four: the four lists were split out of a single module
 * where they shared this constant, and nothing about the split is a reason for
 * them to start disagreeing.
 */
export const ADMIN_PAGE_SIZE = 10;
