"use client";

import { useEffect, type RefObject } from "react";

const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

function isRendered(element: HTMLElement): boolean {
  return element.getClientRects().length > 0;
}

function focusables(root: HTMLElement): HTMLElement[] {
  return Array.from(root.querySelectorAll<HTMLElement>(FOCUSABLE)).filter(isRendered);
}

/**
 * Modal focus handling for the overlays that are not native <dialog> elements: while active,
 * focus moves inside (the first focusable element, else the container), Tab and Shift+Tab cycle
 * within it, and when it closes focus returns to the element that opened it. A container that
 * is not rendered (a breakpoint-hidden variant) is left alone.
 */
export function useFocusTrap(ref: RefObject<HTMLElement | null>, active: boolean): void {
  useEffect(() => {
    const root = ref.current;
    if (!active || !root || !isRendered(root)) return;

    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const first = focusables(root)[0];
    if (first) {
      first.focus({ preventScroll: true });
    } else {
      if (!root.hasAttribute("tabindex")) root.setAttribute("tabindex", "-1");
      root.focus({ preventScroll: true });
    }

    function onKeyDown(event: KeyboardEvent) {
      if (event.key !== "Tab" || !root) return;
      const items = focusables(root);
      if (items.length === 0) {
        event.preventDefault();
        return;
      }
      const current = document.activeElement;
      const atStart = current === items[0] || !root.contains(current);
      const atEnd = current === items[items.length - 1] || !root.contains(current);
      if (event.shiftKey && atStart) {
        event.preventDefault();
        items[items.length - 1].focus();
      } else if (!event.shiftKey && atEnd) {
        event.preventDefault();
        items[0].focus();
      }
    }

    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("keydown", onKeyDown);
      opener?.focus({ preventScroll: true });
    };
  }, [ref, active]);
}
