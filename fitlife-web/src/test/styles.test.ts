/// <reference types="node" />
import { describe, it, expect } from 'vitest'
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join } from 'node:path'

// Tailwind v4 emits utilities inside cascade layers, and unlayered rules beat any
// layered rule. A global `* { margin: 0; padding: 0 }` outside a layer therefore
// silently disables every spacing utility (p-*, m-*, space-*, gap margins).
// Files are read from disk: Vitest stubs CSS imports (even ?raw) to empty strings.
const universalSpacingReset = /(^|[\s,}])\*\s*(,[^{]*)?\{[^}]*\b(margin|padding)\s*:/m

function withoutLayers(css: string): string {
  let result = ''
  let index = 0
  while (index < css.length) {
    const start = css.indexOf('@layer', index)
    if (start === -1) return result + css.slice(index)
    result += css.slice(index, start)
    const open = css.indexOf('{', start)
    if (open === -1) return result
    let depth = 1
    let cursor = open + 1
    while (cursor < css.length && depth > 0) {
      if (css[cursor] === '{') depth++
      else if (css[cursor] === '}') depth--
      cursor++
    }
    index = cursor
  }
  return result
}

function files(dir: string, extension: string): string[] {
  return readdirSync(dir).flatMap((name: string) => {
    const path = join(dir, name)
    return statSync(path).isDirectory() ? files(path, extension) : path.endsWith(extension) ? [path] : []
  })
}

describe('global styles', () => {
  it('keep universal resets out of the unlayered cascade', () => {
    const cssFiles = files('src', '.css')
    const vueFiles = files('src', '.vue')
    const sources = [
      ...cssFiles.map((path) => readFileSync(path, 'utf8')),
      ...vueFiles.flatMap((path) =>
        [...readFileSync(path, 'utf8').matchAll(/<style(?![^>]*scoped)[^>]*>([\s\S]*?)<\/style>/g)].map((m) => m[1] ?? '')
      ),
    ]

    expect(cssFiles.length).toBeGreaterThan(0)
    expect(sources.join('')).toContain('@layer base')
    for (const css of sources) {
      expect(withoutLayers(css)).not.toMatch(universalSpacingReset)
    }
  })
})
