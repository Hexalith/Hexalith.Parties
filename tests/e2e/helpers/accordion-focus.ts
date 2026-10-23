import { expect, type Locator } from '@playwright/test';

/** Requires actual keyboard-focus paint and clearance through composed clipping ancestors. */
export async function expectAccordionFocusClearance(control: Locator): Promise<void> {
  await expect(control).toBeVisible();
  await control.focus();
  await control.page().keyboard.press('Shift+Tab');
  await expect(control).not.toBeFocused();
  const before = await control.evaluate(readFocusPaint);
  await control.page().keyboard.press('Tab');
  await expect(control).toBeFocused();
  const after = await control.evaluate(readFocusPaint);
  const actualIndicators = after.candidates.filter(candidate => {
    const previous = before.candidates.find(item => item.key === candidate.key);
    return candidate.visible && candidate.hasPaint
      && (!previous?.visible || !previous.hasPaint || previous.signature !== candidate.signature);
  });

  expect(actualIndicators.length, 'actual focused control must paint a visible focus indicator').toBeGreaterThan(0);
  expect(after.reserved.owners).toBeGreaterThan(0);
  expect(after.reserved.width).toBeGreaterThan(0);
  expect(after.reserved.offset).toBeGreaterThan(0);
  expect(after.reserved.clippedBy, 'reserved focus ring must clear every clipping ancestor').toEqual([]);
  for (const indicator of actualIndicators) {
    expect(indicator.clippedBy, 'actual focus indicator must clear every clipping ancestor').toEqual([]);
  }
}

function readFocusPaint(element: Element) {
  const transparent = (color: string) => color === 'transparent' || /rgba\([^)]*,\s*0(?:\.0+)?\)$/.test(color);
  const parent = (node: Element): Element | null => node.assignedSlot ?? node.parentElement
    ?? (node.getRootNode() instanceof ShadowRoot ? (node.getRootNode() as ShadowRoot).host : null);
  const clipping = (node: Element, outset: number) => {
    const bounds = node.getBoundingClientRect();
    const clippedBy: string[] = [];
    const visited = new Set<Element>();
    let owners = 0;
    for (let ancestor = parent(node); ancestor && !visited.has(ancestor); ancestor = parent(ancestor)) {
      visited.add(ancestor);
      if (ancestor.tagName === 'FLUENT-ACCORDION-ITEM') owners += 1;
      const style = getComputedStyle(ancestor);
      const owner = ancestor.getBoundingClientRect();
      const left = owner.left + ancestor.clientLeft;
      const top = owner.top + ancestor.clientTop;
      const paintContainment = /(?:paint|strict|content)/.test(style.contain);
      if (((style.overflowX !== 'visible' || paintContainment)
          && (bounds.left - outset < left || bounds.right + outset > left + ancestor.clientWidth))
        || ((style.overflowY !== 'visible' || paintContainment)
          && (bounds.top - outset < top || bounds.bottom + outset > top + ancestor.clientHeight))) {
        clippedBy.push(`${ancestor.tagName}${ancestor.getAttribute('part') ? `[part=${ancestor.getAttribute('part')}]` : ''}`);
      }
    }
    return { owners, clippedBy };
  };
  const shadowOutset = (value: string) => Math.max(0, ...value.split(/,(?![^()]*\))/).map(shadow => {
    if (shadow.includes('inset')) return 0;
    const lengths = shadow.replace(/(?:rgba?|hsla?|color)\([^)]*\)/g, '').match(/-?\d*\.?\d+px/g)?.map(parseFloat) ?? [];
    return Math.max(Math.abs(lengths[0] ?? 0), Math.abs(lengths[1] ?? 0))
      + (2 * (lengths[2] ?? 0)) + (lengths[3] ?? 0);
  }));

  const candidates: Array<{ key: string; visible: boolean; hasPaint: boolean; signature: string; clippedBy: string[] }> = [];
  let index = 0;
  const visit = (node: Element) => {
    const nodeIndex = index++;
    for (const pseudo of [null, '::before', '::after']) {
      const style = getComputedStyle(node, pseudo);
      const width = parseFloat(style.outlineWidth) || 0;
      const outline = width > 0 && !['none', 'hidden'].includes(style.outlineStyle) && !transparent(style.outlineColor);
      const shadowColors = style.boxShadow.match(/rgba?\([^)]*\)/g) ?? [];
      const shadow = style.boxShadow !== 'none' && style.boxShadow !== ''
        && (shadowColors.length === 0 || shadowColors.some(color => !transparent(color)));
      const borders = ['Top', 'Right', 'Bottom', 'Left'].map(side => ({
        width: style.getPropertyValue(`border-${side.toLowerCase()}-width`),
        style: style.getPropertyValue(`border-${side.toLowerCase()}-style`),
        color: style.getPropertyValue(`border-${side.toLowerCase()}-color`),
      }));
      const border = borders.some(item => parseFloat(item.width) > 0 && !['none', 'hidden'].includes(item.style) && !transparent(item.color));
      const visible = node.checkVisibility({ checkOpacity: true, checkVisibilityCSS: true })
        && style.display !== 'none' && style.visibility !== 'hidden' && parseFloat(style.opacity) > 0
        && (!pseudo || (style.content !== 'none' && style.content !== 'normal'));
      const pseudoOutset = pseudo ? Math.max(0, ...['top', 'right', 'bottom', 'left'].map(side => -(parseFloat(style.getPropertyValue(side)) || 0))) : 0;
      const outset = pseudoOutset + Math.max(outline ? Math.max(0, width + (parseFloat(style.outlineOffset) || 0)) : 0,
        shadow ? shadowOutset(style.boxShadow) : 0);
      candidates.push({
        key: `${nodeIndex}${pseudo ?? ''}`,
        visible,
        hasPaint: outline || shadow || border,
        signature: JSON.stringify([style.outlineWidth, style.outlineStyle, style.outlineColor, style.outlineOffset,
          style.boxShadow, borders, style.display, style.visibility, style.opacity]),
        clippedBy: clipping(node, outset).clippedBy,
      });
    }
    for (const child of node.children) visit(child);
    if (node.shadowRoot) for (const child of node.shadowRoot.children) visit(child);
  };
  visit(element);

  // Reserve the module's full outer ring even if Fluent paints an internal one.
  // Put the probe in rendered content, outside a Fluent field's filtered slots.
  const reference = document.createElement('span');
  reference.style.cssText = 'position: absolute; outline: var(--strokeWidthThick) solid var(--colorStrokeFocus2); outline-offset: var(--spacingHorizontalXS);';
  element.closest('fluent-accordion-item')!.querySelector('.fluent-stack-vertical')!.append(reference);
  const style = getComputedStyle(reference);
  const width = parseFloat(style.outlineWidth);
  const offset = parseFloat(style.outlineOffset);
  reference.remove();
  return { candidates, reserved: { width, offset, ...clipping(element, width + offset) } };
}
