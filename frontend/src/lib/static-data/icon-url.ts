// Mirrors src/FFXIV.Export/IconUrl.cs: the search index stores icon ids and this rebuilds the
// XIVAPI asset URL every exported icon follows.
export function iconUrlFromId(icon: number): string {
  const folder = String(Math.floor(icon / 1000) * 1000).padStart(6, "0");
  return `https://v2.xivapi.com/api/asset?path=ui%2Ficon%2F${folder}%2F${String(icon).padStart(6, "0")}.tex&format=png`;
}
