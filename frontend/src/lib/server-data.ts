export interface DataCenterInfo {
  name: string;
  servers: string[];
}

export interface RegionInfo {
  region: string;
  dataCenters: DataCenterInfo[];
}

export const FFXIV_REGIONS: RegionInfo[] = [
  {
    region: "North America",
    dataCenters: [
      { name: "Aether", servers: ["Adamantoise", "Cactuar", "Faerie", "Gilgamesh", "Jenova", "Midgardsormr", "Sargatanas", "Siren"] },
      { name: "Primal", servers: ["Behemoth", "Excalibur", "Exodus", "Famfrit", "Hyperion", "Lamia", "Leviathan", "Ultros"] },
      { name: "Crystal", servers: ["Balmung", "Brynhildr", "Coeurl", "Diabolos", "Goblin", "Malboro", "Mateus", "Zalera"] },
      { name: "Dynamis", servers: ["Halicarnassus", "Maduin", "Marilith", "Seraph", "Cuchulainn", "Kraken", "Rafflesia", "Golem"] },
    ],
  },
  {
    region: "Europe",
    dataCenters: [
      { name: "Chaos", servers: ["Cerberus", "Louisoix", "Moogle", "Omega", "Phantom", "Ragnarok", "Sagittarius", "Spriggan"] },
      { name: "Light", servers: ["Alpha", "Lich", "Odin", "Phoenix", "Raiden", "Shiva", "Twintania", "Zodiark"] },
    ],
  },
  {
    region: "Japan",
    dataCenters: [
      { name: "Elemental", servers: ["Aegis", "Atomos", "Carbuncle", "Garuda", "Gungnir", "Kujata", "Tonberry", "Typhon"] },
      { name: "Gaia", servers: ["Alexander", "Bahamut", "Durandal", "Fenrir", "Ifrit", "Ridill", "Tiamat", "Ultima"] },
      { name: "Mana", servers: ["Anima", "Asura", "Chocobo", "Hades", "Ixion", "Masamune", "Pandaemonium", "Titan"] },
      { name: "Meteor", servers: ["Belias", "Mandragora", "Ramuh", "Shinryu", "Unicorn", "Valefor", "Yojimbo", "Zeromus"] },
    ],
  },
  {
    region: "Oceania",
    dataCenters: [
      { name: "Materia", servers: ["Bismarck", "Ravana", "Sephirot", "Sophia", "Zurvan"] },
    ],
  },
];

export function getDataCenterForServer(serverName: string): string | null {
  for (const region of FFXIV_REGIONS) {
    for (const dc of region.dataCenters) {
      if (dc.servers.some((s) => s.toLowerCase() === serverName.toLowerCase())) {
        return dc.name;
      }
    }
  }
  return null;
}

export function getAllServers(): string[] {
  return FFXIV_REGIONS.flatMap((r) => r.dataCenters.flatMap((dc) => dc.servers));
}
