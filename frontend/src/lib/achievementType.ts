export const ACHIEVEMENT_TYPE_NUM_MAP: Record<number, string> = {
	0: "Milestone",
	1: "Streak",
	2: "Hidden",
};

export function achievementTypeLabel(type: string | number): string {
	if (typeof type === "number")
		return ACHIEVEMENT_TYPE_NUM_MAP[type] ?? "Milestone";
	return type;
}
