export interface CharacterProfileDto {
  id: number;
  characterName: string;
  serverName: string | null;
  dataCenter: string | null;
  lodestoneId: string | null;
  avatarUrl: string | null;
  activeJob: string;
  activeJobDisplayName: string;
  activeJobLevel: number;
  jobLevels: Record<string, JobLevelDto>;
  stats: ProfileStatsDto;
  currentGear: ProfileGearSlotDto[] | null;
  source: string;
  createdAt: string;
  updatedAt: string;
  lastLodestoneSync: string | null;
}

export interface JobLevelDto {
  job: string;
  jobDisplayName: string;
  level: number;
}

export interface ProfileStatsDto {
  craftsmanship: number;
  control: number;
  cp: number;
  gathering: number;
  perception: number;
  gp: number;
  isFromGear: boolean;
}

export interface ProfileGearSlotDto {
  slotKey: string;
  slotName: string;
  equipmentId: number;
  equipmentName: string;
  itemLevel: number;
  iconUrl: string;
}

export interface LodestonePreviewDto {
  characterName: string;
  avatarUrl: string | null;
  serverName: string;
  dataCenter: string | null;
  activeJob: string | null;
  activeJobDisplayName: string | null;
  activeJobLevel: number;
  jobLevels: Record<string, JobLevelDto>;
}

export interface CreateProfileRequest {
  characterName: string;
  serverName?: string;
  dataCenter?: string;
  lodestoneId?: string;
  avatarUrl?: string;
  activeJob: string;
  activeJobLevel: number;
  jobLevels?: Record<string, number>;
  craftsmanship: number;
  control: number;
  cp: number;
  gathering: number;
  perception: number;
  gp: number;
}

export interface UpdateProfileRequest {
  characterName?: string;
  serverName?: string;
  activeJob?: string;
  activeJobLevel?: number;
  craftsmanship?: number;
  control?: number;
  cp?: number;
  gathering?: number;
  perception?: number;
  gp?: number;
}

export interface UpdateJobLevelsRequest {
  jobLevels: Record<string, number>;
}

export interface UpdateCurrentGearRequest {
  slots: Record<string, number>;
}

export interface ImportLodestoneRequest {
  characterName: string;
  serverName: string;
}
