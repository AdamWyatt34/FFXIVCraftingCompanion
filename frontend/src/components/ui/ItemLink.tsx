"use client";

import { useState, useRef, useEffect, useCallback } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ItemIcon } from "./ItemIcon";

interface ItemLinkProps {
  itemId: number;
  itemName: string;
  iconUrl?: string;
  recipeId?: number | null;
  showIcon?: boolean;
  iconSize?: "sm" | "md";
  compact?: boolean;
  className?: string;
}

interface MenuPosition {
  x: number;
  y: number;
}

export function ItemLink({
  itemId,
  itemName,
  iconUrl,
  recipeId,
  showIcon = true,
  iconSize = "sm",
  compact = false,
  className = "",
}: ItemLinkProps) {
  const router = useRouter();
  const [menuPos, setMenuPos] = useState<MenuPosition | null>(null);
  const menuRef = useRef<HTMLDivElement>(null);

  const closeMenu = useCallback(() => setMenuPos(null), []);

  useEffect(() => {
    if (!menuPos) return;
    const handleClose = () => closeMenu();
    document.addEventListener("click", handleClose);
    document.addEventListener("scroll", handleClose, true);
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") closeMenu();
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("click", handleClose);
      document.removeEventListener("scroll", handleClose, true);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [menuPos, closeMenu]);

  // Adjust menu position to stay within viewport
  useEffect(() => {
    if (!menuPos || !menuRef.current) return;
    const menu = menuRef.current;
    const rect = menu.getBoundingClientRect();
    let { x, y } = menuPos;
    if (rect.right > window.innerWidth) x = window.innerWidth - rect.width - 8;
    if (rect.bottom > window.innerHeight) y = window.innerHeight - rect.height - 8;
    if (x < 0) x = 8;
    if (y < 0) y = 8;
    if (x !== menuPos.x || y !== menuPos.y) setMenuPos({ x, y });
  }, [menuPos]);

  const handleMouseEnter = useCallback(() => {
    router.prefetch(`/items/${itemId}`);
  }, [router, itemId]);

  const handleContextMenu = (e: React.MouseEvent) => {
    if (compact) return;
    e.preventDefault();
    e.stopPropagation();
    setMenuPos({ x: e.clientX, y: e.clientY });
  };

  const menuItems: { label: string; href: string }[] = [
    { label: "View Item", href: `/items/${itemId}` },
  ];
  if (recipeId) {
    menuItems.push({ label: "View Recipe", href: `/recipes/${recipeId}` });
    menuItems.push({ label: "Start Journey", href: `/recipes/${recipeId}/journey` });
    menuItems.push({ label: "Simulate", href: `/simulator?recipeId=${recipeId}` });
  }

  return (
    <>
      <Link
        href={`/items/${itemId}`}
        className={`inline-flex items-center gap-1.5 text-sm text-text-primary hover:text-gold transition-colors truncate ${className}`}
        title={itemName}
        onMouseEnter={handleMouseEnter}
        onContextMenu={handleContextMenu}
      >
        {showIcon && iconUrl && (
          <ItemIcon iconUrl={iconUrl} alt={itemName} size={iconSize} />
        )}
        <span className="truncate">{itemName}</span>
      </Link>

      {menuPos && (
        <div
          ref={menuRef}
          className="fixed z-50 min-w-[180px] py-1 bg-bg-surface border border-border rounded-lg shadow-xl"
          style={{ left: menuPos.x, top: menuPos.y }}
          onClick={(e) => e.stopPropagation()}
        >
          {menuItems.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              className="block px-4 py-2 text-sm text-text-primary hover:bg-bg-elevated hover:text-gold transition-colors"
              onClick={closeMenu}
            >
              {item.label}
            </Link>
          ))}
        </div>
      )}
    </>
  );
}
