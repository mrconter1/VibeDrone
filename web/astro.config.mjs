import { defineConfig } from 'astro/config';

// Static landing page. No adapter needed - the site is fully static and fetches the latest release
// client-side. Vercel auto-detects Astro; set the project's Root Directory to "web".
export default defineConfig({
  site: 'https://vibedrone.vercel.app',
});
