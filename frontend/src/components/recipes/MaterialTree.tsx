"use client";

import { useState, useCallback } from "react";
import { ChevronRight, ChevronDown, Maximize2, Minimize2 } from "lucide-react";
import { ItemIcon, ItemLink, SourceBadge } from "@/components/ui";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import type { MaterialTreeNodeDto, CraftType } from "@/types/api";

interface MaterialTreeProps {
  tree: MaterialTreeNodeDto;
}

function nodeKey(path: string, itemId: number): string {
  return `${path}:${itemId}`;
}

function collectExpandable(
  node: MaterialTreeNodeDto,
  path: string,
  set: Set<string>,
) {
  if (node.children.length > 0) {
    set.add(nodeKey(path, node.itemId));
    node.children.forEach((child, i) =>
      collectExpandable(child, `${path}.${i}`, set),
    );
  }
}

const depthBgClasses = [
  "",
  "bg-blue-500/[0.03]",
  "bg-violet-500/[0.03]",
  "bg-emerald-500/[0.03]",
  "bg-amber-500/[0.03]",
];

function getDepthBg(depth: number): string {
  return depthBgClasses[Math.min(depth, depthBgClasses.length - 1)];
}

export function MaterialTree({ tree }: MaterialTreeProps) {
  const [expandedNodes, setExpandedNodes] = useState<Set<string>>(() => {
    const initial = new Set<string>();
    initial.add(nodeKey("root", tree.itemId));
    for (let i = 0; i < tree.children.length; i++) {
      const child = tree.children[i];
      if (child.children.length > 0) {
        initial.add(nodeKey(`root.${i}`, child.itemId));
      }
    }
    return initial;
  });

  const expandAll = useCallback(() => {
    const all = new Set<string>();
    collectExpandable(tree, "root", all);
    setExpandedNodes(all);
  }, [tree]);

  const collapseAll = useCallback(() => {
    setExpandedNodes(new Set<string>());
  }, []);

  const toggle = useCallback((key: string) => {
    setExpandedNodes((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }, []);

  return (
    <div>
      <div className="flex gap-2 mb-3">
        <button
          onClick={expandAll}
          className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg bg-bg-elevated text-text-secondary hover:text-gold transition-colors"
        >
          <Maximize2 size={14} />
          Expand All
        </button>
        <button
          onClick={collapseAll}
          className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg bg-bg-elevated text-text-secondary hover:text-gold transition-colors"
        >
          <Minimize2 size={14} />
          Collapse All
        </button>
      </div>

      <TreeNode
        node={tree}
        path="root"
        isLast={true}
        expandedNodes={expandedNodes}
        onToggle={toggle}
      />
    </div>
  );
}

interface TreeNodeProps {
  node: MaterialTreeNodeDto;
  path: string;
  isLast: boolean;
  expandedNodes: Set<string>;
  onToggle: (key: string) => void;
}

function TreeNode({
  node,
  path,
  isLast,
  expandedNodes,
  onToggle,
}: TreeNodeProps) {
  const key = nodeKey(path, node.itemId);
  const hasChildren = node.children.length > 0;
  const isExpanded = expandedNodes.has(key);
  const craftInfo =
    node.recipe ? CRAFT_TYPE_INFO[node.recipe.craftType as CraftType] : null;

  return (
    <div className={`relative ${node.depth > 0 ? "ml-6" : ""}`}>
      {/* Tree connector lines */}
      {node.depth > 0 && (
        <>
          <div
            className={`absolute left-[-16px] top-0 w-px bg-[#3a3a5c] ${
              isLast ? "h-[22px]" : "h-full"
            }`}
          />
          <div className="absolute left-[-16px] top-[22px] w-4 h-px bg-[#3a3a5c]" />
        </>
      )}

      {/* Node row */}
      <div
        className={`group flex items-center gap-2 p-2 rounded-lg ${getDepthBg(
          node.depth,
        )} hover:bg-bg-elevated/50 transition-colors min-h-[44px]`}
      >
        {hasChildren ? (
          <button
            onClick={() => onToggle(key)}
            className="w-6 h-6 flex items-center justify-center shrink-0 text-text-muted hover:text-gold transition-colors"
            aria-label={isExpanded ? "Collapse" : "Expand"}
          >
            {isExpanded ? (
              <ChevronDown size={16} />
            ) : (
              <ChevronRight size={16} />
            )}
          </button>
        ) : (
          <div className="w-6 shrink-0" />
        )}

        <ItemIcon iconUrl={node.iconUrl} alt={node.itemName} size="sm" />

        <ItemLink
          itemId={node.itemId}
          itemName={node.itemName}
          iconUrl={node.iconUrl}
          recipeId={node.recipe?.recipeId}
          showIcon={false}
          className="truncate"
        />

        <span className="text-sm text-text-muted font-mono shrink-0">
          x{node.quantity}
        </span>

        <SourceBadge sourceType={node.primarySource} className="shrink-0" />

        {craftInfo && (
          <span
            className={`text-xs ${craftInfo.colorClass} shrink-0 hidden sm:inline`}
          >
            {craftInfo.name} Lv.{node.recipe!.recipeLevel}
          </span>
        )}
      </div>

      {/* Source detail on hover (desktop only) */}
      {node.sources.length > 0 && node.sources[0].description && (
        <div className="ml-8 px-2 pb-1 text-xs text-text-muted opacity-0 group-hover:opacity-100 transition-opacity truncate hidden sm:block pointer-events-none">
          {node.sources[0].description}
          {node.sources[0].zone && ` \u2014 ${node.sources[0].zone}`}
        </div>
      )}

      {/* Children */}
      {hasChildren && isExpanded && (
        <div className="relative">
          {node.children.map((child, i) => (
            <TreeNode
              key={`${child.itemId}-${i}`}
              node={child}
              path={`${path}.${i}`}
              isLast={i === node.children.length - 1}
              expandedNodes={expandedNodes}
              onToggle={onToggle}
            />
          ))}
        </div>
      )}
    </div>
  );
}
