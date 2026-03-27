export default function Footer() {
  return (
    <footer className="bg-slate-900 text-slate-300">
      <div className="max-w-7xl mx-auto px-4 py-12 sm:px-6 lg:px-8">
        
        {/* 12-Column Responsive Grid */}
        <div className="grid grid-cols-12 gap-8">
          
          {/* Brand Section - 12 Spalten Mobile, 4 Desktop */}
          <div className="col-span-12 lg:col-span-4">
            <h2 className="text-white text-2xl font-bold mb-4">Einsatzbereit</h2>
            <p className="text-sm leading-relaxed max-w-xs">
                Einsatzbereit bringt engagierte Helfer:innen mit regionalen Bedarfen zusammen. Spontan, schnell und wirksam.
            </p>
          </div>

          {/* Navigation Links - 6 Spalten Tablet, 2 Desktop */}
          <div className="col-span-6 md:col-span-4 lg:col-span-2">
            <h3 className="text-white font-semibold mb-4 uppercase text-xs tracking-wider">Produkt</h3>
            <ul className="space-y-2 text-sm">
              <li><a href="#" className="hover:text-indigo-400 transition-colors">Features</a></li>
              <li><a href="#" className="hover:text-indigo-400 transition-colors">Preise</a></li>
              <li><a href="#" className="hover:text-indigo-400 transition-colors">API</a></li>
            </ul>
          </div>

          {/* Company Links - 6 Spalten Tablet, 2 Desktop */}
          <div className="col-span-6 md:col-span-4 lg:col-span-2">
            <h3 className="text-white font-semibold mb-4 uppercase text-xs tracking-wider">Unternehmen</h3>
            <ul className="space-y-2 text-sm">
              <li><a href="#" className="hover:text-indigo-400 transition-colors">Über uns</a></li>
              <li><a href="#" className="hover:text-indigo-400 transition-colors">Karriere</a></li>
              <li><a href="#" className="hover:text-indigo-400 transition-colors">Blog</a></li>
            </ul>
          </div>

          {/* Newsletter Section - 12 Spalten Tablet/Mobile, 4 Desktop */}
          <div className="col-span-12 md:col-span-12 lg:col-span-4">
            <h3 className="text-white font-semibold mb-4 uppercase text-xs tracking-wider">Newsletter</h3>
            <p className="text-sm mb-4">Erhalte die neuesten Updates direkt in dein Postfach.</p>
            <form className="flex flex-col sm:flex-row gap-2">
              <input 
                type="email" 
                placeholder="Deine E-Mail" 
                className="bg-slate-800 border border-slate-700 rounded-md px-4 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 w-full text-white"
              />
              <button className="bg-indigo-600 hover:bg-indigo-500 text-white px-4 py-2 rounded-md text-sm font-medium transition-colors">
                Abonnieren
              </button>
            </form>
          </div>

        </div>

        {/* Bottom Bar */}
        <div className="mt-12 pt-8 border-t border-slate-800 flex flex-col md:flex-row justify-between items-center gap-4 text-xs">
          <p>&copy; {new Date().getFullYear()} BrandName Inc. Alle Rechte vorbehalten.</p>
          <div className="flex space-x-6">
            <a href="#" className="hover:text-white transition-colors">Datenschutz</a>
            <a href="#" className="hover:text-white transition-colors">Impressum</a>
            <a href="#" className="hover:text-white transition-colors">Cookies</a>
          </div>
        </div>
      </div>
    </footer>
  );
}