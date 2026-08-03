// BadgeCatalogEntry.type serializes as the raw AchievementType enum ordinal (no
// JsonStringEnumConverter on the backend), while AchievementSummary.type is already
// stringified server-side (AchievementReadRepository projects a.Type.ToString()). This
// map has to mirror Domain.Achievements.AchievementType's declaration order exactly -
// Milestone, Streak, Hidden - or a numeric type resolves to the wrong name.
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
