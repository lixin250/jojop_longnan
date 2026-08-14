export type RoleStatUpdate = {
    id: string;
    base_hp: number;
    base_atk: number;
    base_move: number;
    base_defense: number;
    crit_rate: number;
    crit_damage: number;
    attack_interval: number;
    faction_tags?: string;
    skill_ids?: string;
    avatar_loc?: string;
};
export declare function batchUpdateRoleStats(updates: RoleStatUpdate[]): Promise<{
    workbook: string;
    sheet: string;
    backup: string;
    updated: number;
    changed: number;
}>;
export declare function validateConfig(): Promise<{
    ok: boolean;
    counts: {
        roles: number;
        skills: number;
        effects: number;
        fusions: number;
        education_programs: number;
        life_routes: number;
        career_growth: number;
        run_chapter_rules: number;
        rogue_rewards: number;
        equipment: number;
        character_encounters: number;
        run_events: number;
        timeline_events: number;
    };
    issues: {
        level: "error" | "warning";
        code: string;
        message: string;
    }[];
}>;
export declare function runLuban(): {
    ok: boolean;
    exitCode: number | null;
    output: string;
};
