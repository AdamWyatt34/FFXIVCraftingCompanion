export interface MarketBoardDataDto {
  itemId: number;
  averagePriceNQ: number;
  averagePriceHQ: number;
  minPriceNQ: number;
  minPriceHQ: number;
  currentListingCount: number;
  lastUploadTime: string;
  recentListings: MarketListingDto[];
  isStale: boolean;
}

export interface MarketListingDto {
  pricePerUnit: number;
  quantity: number;
  total: number;
  isHQ: boolean;
  retainerName: string;
}

export interface CostEstimateDto {
  worldOrDc: string;
  generatedAt: string;
  materialCosts: MaterialCostDto[];
  totalVendorCost: number;
  totalMarketBoardCost: number;
  totalEstimatedCost: number;
  finalItemMarketPrice: number | null;
  craftingIsCheaper: boolean | null;
  savings: number | null;
  warnings?: string[];
}

export interface MaterialCostDto {
  itemId: number;
  itemName: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  priceSource: PriceSource;
  isStalePrice: boolean;
  listingCount: number;
  lastUploadTime?: string;
}

export type PriceSource = "Vendor" | "MarketBoard" | "NonGil" | "Unknown";

export interface WorldGroupDto {
  region: string;
  dataCenters: DataCenterDto[];
}

export interface DataCenterDto {
  name: string;
  worlds: string[];
}
