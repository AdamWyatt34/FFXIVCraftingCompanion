export interface GatheringGuideDto {
  zoneGroups: GatheringZoneGroupDto[];
  timedNodes: TimedNodeEntryDto[];
  crystalSummary: CrystalRequirementDto[];
  summary: GatheringGuideSummaryDto;
}

export interface GatheringZoneGroupDto {
  zoneName: string;
  items: GatheringGuideItemDto[];
}

export interface GatheringGuideItemDto {
  itemId: number;
  itemName: string;
  iconUrl: string;
  quantity: number;
  gatheringClass: string;
  gatheringLevel: number;
  nodeType: string;
  coordinateX: number | null;
  coordinateY: number | null;
  timeWindow: EorzeanTimeWindowDto | null;
  isHidden: boolean;
}

export interface EorzeanTimeWindowDto {
  startHour: number;
  endHour: number;
  isCurrentlyActive: boolean;
  timeUntil: string | null;
}

export interface TimedNodeEntryDto {
  itemId: number;
  itemName: string;
  iconUrl: string;
  quantity: number;
  zoneName: string;
  gatheringClass: string;
  gatheringLevel: number;
  nodeType: string;
  startHour: number;
  endHour: number;
  isCurrentlyActive: boolean;
  timeUntil: string | null;
  coordinateX: number | null;
  coordinateY: number | null;
}

export interface CrystalRequirementDto {
  itemId: number;
  itemName: string;
  iconUrl: string;
  quantity: number;
  element: string;
  tier: string;
}

export interface GatheringGuideSummaryDto {
  totalZones: number;
  totalItems: number;
  timedNodeCount: number;
  totalCrystalTypes: number;
}

export interface GatheringGuideItemInput {
  itemId: number;
  quantity: number;
}

export interface GenerateGatheringGuideRequest {
  items: GatheringGuideItemInput[];
}

export interface EorzeanTimeResponse {
  currentEorzeanHour: number;
}
