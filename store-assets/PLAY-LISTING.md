# Play Console — store listing for PurePrep 1.4

Copy and paste from here. Every count below is in Unicode code points, which is how Play counts,
and is checked by a script rather than by eye:

```bash
node store-assets/_src/check-listing.mjs
```

Screenshots are not in this file yet. They get captured from the final 1.4 build on the phone. The
slot plan is at the end.

---

## 1. English (en-US) → Store listing → App details

### App name — 29 / 30

```
PurePrep: Save & Cook Recipes
```

Alternates:

#### Alternate A — 30 / 30

```
PurePrep: Recipe Saver & Timer
```

#### Alternate B — 30 / 30

```
PurePrep: Recipe Box & Clipper
```

**Why this name.** The title is Play's heaviest ranking signal. Play indexes each word on its own, so
"Save & Cook Recipes" still matches searches for *save recipes*, *recipe saver* and *cook recipes*,
and it tells a stranger what the app does. It reads as a sentence, not a list of keywords.

**Why it is not "PurePrep: Ad-Free Recipe Keeper".** That was the brief's example, and there are
two problems with it:
1. It is **31** characters, one over the limit.
2. Google Play's metadata policy does not allow promotional terms such as "free" or "no ads" in the
   app title (or in the icon or developer name). "Ad-Free" would risk a rejection. The no-ads
   message goes in the short description, the feature graphic and the full description, where it
   is allowed.

"Recipe Keeper" is also the name of an existing app. Putting it in our title invites confusion and
trademark complaints, so it stays out of the title. It appears only as a generic phrase in the full
description.

### Short description — 79 / 80

```
Save recipes from any website – just ingredients and steps. No ads, no account.
```

### Full description — 3,393 / 4,000

```
The recipe, without the noise.

PurePrep is a recipe keeper for people who just want to cook. Share a link from any website and PurePrep saves only what you cook from: the ingredients, the steps, the timers and a photo. No ads, no pop-ups, no autoplay video, no ten-paragraph life story.

No ads. No account. No tracking.

SAVE RECIPES FROM ANY WEBSITE
• Share to import: tap Share in your browser or any app that shares links, and pick PurePrep.
• Copied a link? Open PurePrep and it offers to import it.
• Search the web without leaving the app, then tap "Add to PurePrep" on a recipe page.
• Snap a cookbook page or a screenshot, or paste recipe text from a note or a message.
• More than a recipe clipper: PurePrep reads the servings, prep and cook times, named timers like "Fry onion · 10–12 min", and which ingredients each step uses.
• No photo on the page? You get an AI-made picture of the dish instead of a blank card.

COOK STEP BY STEP
• Focus Mode shows one step at a time, always fitted to the screen, with "You'll need" listing that step's ingredients.
• Cooking timers live in the steps: tap one to start it. Run several at once, and a timers bar follows you around the app.
• Have the step read aloud (and stop it any time), or move through the recipe hands-free with voice commands.
• The screen stays awake, so nothing dims under floury hands.

SCALE, CONVERT, ORGANIZE
• Scale servings by people: Serves − 4 +, and every amount updates, in the fractions a cook would write.
• Switch any recipe between metric and US units.
• A recipe organizer that stays out of the way: favorites, Want to cook / Cooked, your own notes, and sorting by recently added, recently cooked or A–Z.
• Edit anything: one row per ingredient and step, drag to reorder, add your own photo.
• Type in family recipes by hand.
• Translate a recipe into your language. PurePrep speaks English, Polish, German, French, Spanish, Italian and Dutch.
• Dark and light themes.

YOURS, OFFLINE
• Your recipe box lives on your phone. Saved recipes open without internet: in the kitchen, on holiday, on a plane.
• Back up your whole library, photos included, to one file and restore it on a new phone.

NO ADS. NO ACCOUNT. NO TRACKING.
• No sign-up, no email, no password.
• No ads, and no third-party analytics or tracking SDKs.
• Your recipes, notes and photos stay on your device. An import sends only what you chose to import, to be read by AI. The clean recipe comes back and is saved on your phone.
• Web search opens Google and recipe sites as they are, with their own cookies. PurePrep adds no tracking of its own.

WHAT'S FREE
Saving, editing, scaling, timers, Focus Mode and adding recipes by hand are free, with no limit on how many recipes you keep. Importing and translating use AI that runs on paid servers, so they use Smart Credits: 1 for a link or pasted text, 2 for a photo, and 1 to translate a recipe into a new language (the translation is kept, so you pay for it once). You start with 10 free Smart Credits. After that, credits come in small packs in the app. No subscription.

WHY I BUILT IT
I wanted my recipes in one quiet place, without scrolling past ads and life stories to find out how much flour goes in. So I built PurePrep, and I cook from it every day. If something doesn't work for you, write to me: andrzej@lechdigital.nl

Privacy policy: https://pureprep.lechdigital.nl/privacy
```

---

## 2. Polish (pl-PL) → Store listing → App details

This is written in Polish, not translated from the English. The UI terms match the app's
`AppResources.pl.resx`: Tryb Skupienia, Potrzebujesz, Do ugotowania / Ugotowane, Dodaj do PurePrep,
Dla − 4 + osób, Smart Credits. The copy uses gender-neutral forms such as "Masz skopiowany link?",
not "Skopiowałeś".

### Nazwa aplikacji — 27 / 30

```
PurePrep: Książka kucharska
```

#### Alternatywa A — 26 / 30

```
PurePrep: Zapisuj przepisy
```

#### Alternatywa B — 30 / 30

```
PurePrep: Przepisy i minutniki
```

"Książka kucharska" is how Polish users search for this kind of app. "Przepisy" on its own is huge
and generic, and recipe sites dominate it. Same policy point as in English: no "bez reklam" in the
title.

### Krótki opis — 78 / 80

```
Zapisuj przepisy z każdej strony – same składniki i kroki. Bez reklam i konta.
```

### Pełny opis — 3,819 / 4,000

```
Przepis. Bez zbędnego szumu.

PurePrep to książka kucharska w telefonie dla tych, którzy po prostu chcą gotować. Udostępnij link z dowolnej strony, a PurePrep zachowa tylko to, z czego się gotuje: składniki, kroki, minutniki i zdjęcie. Bez reklam, wyskakujących okienek, filmów, które włączają się same, i bez historii życia autora na dziesięć akapitów.

Bez reklam. Bez konta. Bez śledzenia.

ZAPISUJ PRZEPISY Z KAŻDEJ STRONY
• Udostępnij, żeby zaimportować: w przeglądarce albo innej aplikacji stuknij „Udostępnij” i wybierz PurePrep.
• Masz skopiowany link? Otwórz PurePrep, a aplikacja sama zaproponuje import.
• Szukaj przepisów w internecie bez wychodzenia z aplikacji i stuknij „Dodaj do PurePrep” na stronie z przepisem.
• Zrób zdjęcie strony z książki kucharskiej albo zrzut ekranu, lub wklej tekst przepisu z notatki czy wiadomości.
• PurePrep rozumie przepis: liczbę porcji, czas przygotowania i gotowania, nazwane minutniki w rodzaju „Podsmaż cebulę · 10–12 min” i to, których składników potrzebujesz w danym kroku.
• Strona nie ma zdjęcia? Zamiast pustej karty dostaniesz obrazek potrawy wygenerowany przez AI.

GOTUJ KROK PO KROKU
• Tryb Skupienia pokazuje jeden krok naraz, zawsze dopasowany do ekranu, a pod nim „Potrzebujesz”, czyli składniki do tego kroku.
• Minutniki są wbudowane w kroki: stuknij i odliczanie rusza. Kilka może działać naraz, a pasek minutników widać w całej aplikacji.
• Posłuchaj kroku na głos (i zatrzymaj w każdej chwili) albo przechodź między krokami poleceniami głosowymi, bez dotykania ekranu.
• Ekran nie gaśnie, więc nie trzeba go budzić ręką w mące.

SKALUJ, PRZELICZAJ, PORZĄDKUJ
• Skaluj na liczbę osób: Dla − 4 + osób, a wszystkie ilości przeliczają się same, w ułamkach, jakie zapisałby kucharz.
• Przełączaj przepis między jednostkami metrycznymi a amerykańskimi.
• Porządek bez wysiłku: ulubione, „Do ugotowania” i „Ugotowane”, własne notatki oraz sortowanie według daty dodania, ostatniego gotowania albo alfabetycznie.
• Edytuj wszystko: osobny wiersz na każdy składnik i krok, przeciąganie, żeby zmienić kolejność, własne zdjęcie.
• Wpisz ręcznie przepisy z rodzinnego zeszytu.
• Przetłumacz przepis na swój język. PurePrep mówi po polsku, angielsku, niemiecku, francusku, hiszpańsku, włosku i niderlandzku.
• Motyw ciemny i jasny.

TWOJE, TAKŻE OFFLINE
• Przepisy mieszkają w Twoim telefonie. Zapisane otwierają się bez internetu: w kuchni, na działce, w samolocie.
• Zrób kopię zapasową całej kolekcji, razem ze zdjęciami, w jednym pliku i przywróć ją na nowym telefonie.

BEZ REKLAM. BEZ KONTA. BEZ ŚLEDZENIA.
• Bez rejestracji, e-maila i hasła.
• Bez reklam i bez zewnętrznych narzędzi analitycznych czy śledzących.
• Przepisy, notatki i zdjęcia zostają na Twoim urządzeniu. Import wysyła do odczytania przez AI tylko to, co wybierzesz. Gotowy przepis wraca i zapisuje się w telefonie.
• Wyszukiwarka otwiera Google i strony z przepisami takie, jakie są, z ich własnymi plikami cookie. PurePrep nie dokłada własnego śledzenia.

CO JEST ZA DARMO
Zapisywanie, edycja, skalowanie, minutniki, Tryb Skupienia i ręczne dodawanie przepisów są darmowe, bez limitu przepisów. Import i tłumaczenie korzystają z AI na płatnych serwerach, więc zużywają Smart Credits: 1 za link lub wklejony tekst, 2 za zdjęcie i 1 za tłumaczenie przepisu na nowy język (tłumaczenie zostaje zapisane, więc płacisz raz). Na start dostajesz 10 darmowych Smart Credits, a kolejne kupisz w małych pakietach w aplikacji. Bez subskrypcji.

DLACZEGO GO ZROBIŁEM
Chciałem mieć swoje przepisy w jednym spokojnym miejscu, bez przewijania reklam i historii życia tylko po to, żeby sprawdzić, ile dać mąki. Dlatego zbudowałem PurePrep i sam gotuję z niego codziennie. Jeśli coś u Ciebie nie działa, napisz do mnie: andrzej@lechdigital.nl

Polityka prywatności: https://pureprep.lechdigital.nl/privacy
```

---

## 3. German (de-DE) → Store listing → App details

Written in German, not translated from the English. The UI terms match `AppResources.de.resx`:
Fokus-Modus, Du brauchst, Möchte ich kochen / Gekocht, Zu PurePrep hinzufügen, Für − 4 + Personen,
Smart Credits. The app says "du", so the listing does too. Search words: *Rezepte speichern*,
*Rezept-App*, *Kochbuch*, *Rezeptverwaltung*, *Kochbuch-App*.

### App-Name — 28 / 30

```
PurePrep: Kochbuch & Rezepte
```

#### Alternative A — 27 / 30

```
PurePrep: Rezepte speichern
```

#### Alternative B — 28 / 30

```
PurePrep: Rezept-App & Timer
```

"Kochbuch" plus "Rezepte" covers *Kochbuch App*, *Rezepte App* and *digitales Kochbuch*;
"Rezepte speichern" is in the short description and a heading, so it is indexed anyway. No "ohne
Werbung" or "kostenlos" in the title.

### Kurzbeschreibung — 77 / 80

```
Rezepte von jeder Website speichern – nur Zutaten und Schritte. Ohne Werbung.
```

### Vollständige Beschreibung — 3,997 / 4,000

```
Das Rezept. Ohne das Drumherum.

PurePrep ist die Rezept-App und dein digitales Kochbuch für alle, die einfach kochen wollen. Teile einen Link von einer beliebigen Website, und PurePrep speichert nur das, womit du kochst: Zutaten, Schritte, Timer und ein Foto. Keine Werbung, keine Pop-ups, keine Videos, die von selbst starten, keine Lebensgeschichte über zehn Absätze.

Keine Werbung. Kein Konto. Kein Tracking.

REZEPTE VON JEDER WEBSITE SPEICHERN
• Teilen zum Importieren: Tippe im Browser oder in einer anderen App auf „Teilen“ und wähle PurePrep.
• Link kopiert? Öffne PurePrep, und die App bietet dir den Import an.
• Suche Rezepte im Web, ohne die App zu verlassen, und tippe auf der Rezeptseite auf „Zu PurePrep hinzufügen“.
• Fotografiere eine Kochbuchseite oder einen Screenshot, oder füge Rezepttext aus einer Notiz oder Nachricht ein.
• Mehr als ein Lesezeichen: PurePrep erkennt Portionen, Vorbereitungs- und Kochzeit, benannte Timer wie „Zwiebel anbraten · 10–12 Min.“ und welche Zutaten jeder Schritt braucht.
• Kein Foto auf der Seite? Dann gibt es statt einer leeren Karte ein KI-Bild des Gerichts.

SCHRITT FÜR SCHRITT KOCHEN
• Der Fokus-Modus zeigt einen Schritt nach dem anderen, immer passend für den Bildschirm, und unter „Du brauchst“ die Zutaten für genau diesen Schritt.
• Die Küchentimer stecken in den Schritten: antippen, und sie laufen. Mehrere laufen gleichzeitig, und eine Timerleiste begleitet dich durch die App.
• Lass dir den Schritt vorlesen (und stoppe jederzeit) oder geh freihändig per Sprachbefehl durch das Rezept.
• Der Bildschirm bleibt an, auch mit Mehl an den Händen.

SKALIEREN, UMRECHNEN, ORDNEN
• Nach Personen skalieren: Für − 4 + Personen, und alle Mengen passen sich an, in Brüchen, wie man sie in der Küche schreibt.
• Jedes Rezept zwischen metrischen und US-Einheiten umschalten.
• Rezeptverwaltung, die nicht im Weg steht: Favoriten, „Möchte ich kochen“ / „Gekocht“, eigene Notizen und Sortierung nach zuletzt hinzugefügt, zuletzt gekocht oder A–Z.
• Alles bearbeiten: eine Zeile pro Zutat und Schritt, per Ziehen neu ordnen, eigenes Foto hinzufügen.
• Familienrezepte einfach selbst eintippen.
• Rezepte in deine Sprache übersetzen. PurePrep spricht Deutsch, Englisch, Polnisch, Französisch, Spanisch, Italienisch und Niederländisch.
• Dunkles und helles Design.

DEIN KOCHBUCH, AUCH OFFLINE
• Deine Rezeptsammlung liegt auf deinem Handy. Gespeicherte Rezepte öffnen sich ohne Internet: in der Küche, im Urlaub, im Flugzeug.
• Sichere deine ganze Sammlung samt Fotos in einer Datei und stelle sie auf einem neuen Handy wieder her.

KEINE WERBUNG. KEIN KONTO. KEIN TRACKING.
• Keine Registrierung, keine E-Mail, kein Passwort.
• Keine Werbung und keine Analyse- oder Tracking-SDKs von Drittanbietern.
• Rezepte, Notizen und Fotos bleiben auf deinem Gerät. Ein Import sendet nur das, was du importieren willst, zum Auslesen an die KI. Das fertige Rezept kommt zurück und wird auf deinem Handy gespeichert.
• Die Websuche öffnet Google und Rezeptseiten unverändert, mit ihren eigenen Cookies. PurePrep fügt kein eigenes Tracking hinzu.

WAS KOSTENLOS IST
Speichern, Bearbeiten, Skalieren, Timer, Fokus-Modus und eigene Rezepte von Hand sind kostenlos, ohne Limit bei der Zahl der Rezepte. Import und Übersetzung laufen über KI auf bezahlten Servern, daher kosten sie Smart Credits: 1 für einen Link oder eingefügten Text, 2 für ein Foto und 1, um ein Rezept in eine neue Sprache zu übersetzen (die Übersetzung bleibt gespeichert, du zahlst also nur einmal). Zum Start bekommst du 10 kostenlose Smart Credits. Danach gibt es Credits in kleinen Paketen in der App. Kein Abo.

WARUM ICH ES GEBAUT HABE
Ich wollte meine Rezepte an einem ruhigen Ort haben, ohne mich durch Werbung und Lebensgeschichten zu scrollen, bis ich weiß, wie viel Mehl hineinkommt. Also habe ich PurePrep gebaut, und ich koche selbst jeden Tag damit. Wenn etwas nicht klappt, schreib mir: andrzej@lechdigital.nl

Datenschutzerklärung: https://pureprep.lechdigital.nl/privacy
```

---

## 4. French (fr-FR) → Store listing → App details

Written in French, not translated from the English. The UI terms match `AppResources.fr.resx`:
mode Focus, Il vous faut, À cuisiner / Cuisinées, Ajouter à PurePrep, Pour − 4 + personnes, Smart
Credits. The app says "vous", so the listing does too. French typography keeps the space before
":", "?" and inside « ». Search words: *carnet de recettes*, *enregistrer des recettes*, *livre de
recettes*, *recettes de cuisine*, *appli recettes*.

### Nom de l'application — 29 / 30

```
PurePrep : Carnet de recettes
```

#### Variante A — 28 / 30

```
PurePrep : Livre de recettes
```

#### Variante B — 30 / 30

```
PurePrep : Recettes de cuisine
```

"Carnet de recettes" is the everyday French phrase for a personal recipe collection and is what
people type; "livre de recettes" and "enregistrer des recettes" are in the descriptions. No "sans
pub" or "gratuit" in the title.

### Description courte — 76 / 80

```
Enregistrez vos recettes du web : ingrédients et étapes. Sans pub ni compte.
```

### Description complète — 3,950 / 4,000

```
La recette, sans le bruit.

PurePrep est un carnet de recettes pour qui veut simplement cuisiner. Partagez un lien depuis n'importe quel site et PurePrep n'enregistre que ce qui sert en cuisine : les ingrédients, les étapes, les minuteurs et une photo. Pas de pub, pas de pop-up, pas de vidéo qui se lance toute seule, pas de roman en dix paragraphes.

Sans pub. Sans compte. Sans pistage.

ENREGISTRER DES RECETTES DE TOUT SITE
• Partager pour importer : dans votre navigateur ou une autre appli, touchez « Partager » et choisissez PurePrep.
• Vous avez copié un lien ? Ouvrez PurePrep : l'appli vous propose de l'importer.
• Cherchez sur le web sans quitter l'appli, puis touchez « Ajouter à PurePrep » sur la recette.
• Photographiez une page de livre de cuisine ou une capture d'écran, ou collez le texte d'une recette.
• Bien plus qu'un marque-page : PurePrep repère les portions, les temps de préparation et de cuisson, les minuteurs nommés comme « Faire revenir l'oignon · 10–12 min » et les ingrédients de chaque étape.
• Pas de photo sur la page ? Une image du plat créée par IA remplace la carte vide.

CUISINER ÉTAPE PAR ÉTAPE
• Le mode Focus affiche une étape à la fois, toujours ajustée à l'écran, avec « Il vous faut » : les ingrédients de cette étape.
• Les minuteurs sont dans les étapes : touchez-en un pour le lancer. Plusieurs peuvent tourner à la fois, et une barre de minuteurs vous suit dans toute l'appli.
• Faites lire l'étape à voix haute (et arrêtez quand vous voulez), ou avancez mains libres par commandes vocales.
• L'écran reste allumé, même les mains pleines de farine.

ADAPTER, CONVERTIR, ORGANISER
• Adaptez la recette au nombre de convives : Pour − 4 + personnes, et chaque quantité suit, en fractions comme les écrirait un cuisinier.
• Passez toute recette des unités métriques aux unités américaines.
• Tout bien rangé : favoris, « À cuisiner » / « Cuisinées », vos notes, et tri par ajout récent, dernière cuisson ou A–Z.
• Modifiez tout : une ligne par ingrédient et par étape, glissez pour réorganiser, ajoutez votre propre photo.
• Saisissez à la main les recettes de famille.
• Traduisez une recette dans votre langue. PurePrep parle français, anglais, polonais, allemand, espagnol, italien et néerlandais.
• Thèmes sombre et clair.

À VOUS, MÊME HORS LIGNE
• Votre livre de recettes vit dans votre téléphone. Les recettes enregistrées s'ouvrent sans internet : en cuisine, en vacances, dans l'avion.
• Sauvegardez toute votre collection, photos comprises, dans un seul fichier, et restaurez-la sur un nouveau téléphone.

SANS PUB. SANS COMPTE. SANS PISTAGE.
• Pas d'inscription, pas d'e-mail, pas de mot de passe.
• Pas de pub, et aucun SDK tiers d'analyse ou de pistage.
• Vos recettes, notes et photos restent sur votre appareil. Un import n'envoie que ce que vous choisissez d'importer, pour que l'IA le lise. La recette propre revient et s'enregistre sur votre téléphone.
• La recherche web ouvre Google et les sites de recettes tels quels, avec leurs propres cookies. PurePrep n'ajoute aucun pistage.

CE QUI EST GRATUIT
Enregistrer, modifier, adapter les portions, les minuteurs, le mode Focus et la saisie manuelle sont gratuits, sans limite de recettes. L'import et la traduction passent par une IA sur des serveurs payants, ils utilisent donc des Smart Credits : 1 pour un lien ou un texte collé, 2 pour une photo, et 1 pour traduire une recette dans une nouvelle langue (la traduction est conservée, vous ne la payez qu'une fois). Vous commencez avec 10 Smart Credits offerts. Ensuite, les crédits s'achètent en petits packs dans l'appli. Sans abonnement.

POURQUOI JE L'AI CRÉÉE
Je voulais mes recettes dans un endroit calme, sans faire défiler des pubs et des histoires de vie pour savoir combien de farine mettre. Alors j'ai créé PurePrep, et je cuisine avec tous les jours. Un souci ? Écrivez-moi : andrzej@lechdigital.nl

Politique de confidentialité : https://pureprep.lechdigital.nl/privacy
```

---

## 5. Spanish (es-ES) → Store listing → App details

Written in Spanish (Spain), not translated from the English. The UI terms match
`AppResources.es.resx`: modo Concentración, Necesitarás, Por cocinar / Cocinadas, Añadir a PurePrep,
Para − 4 + personas, Smart Credits. The app says "tú", so the listing does too, with Spain's
vocabulary (móvil, vídeo, «»). Search words: *guardar recetas*, *recetario*, *libro de recetas*,
*app de recetas*, *recetas de cocina*.

### Nombre de la aplicación — 29 / 30

```
PurePrep: Recetario de cocina
```

#### Alternativa A — 25 / 30

```
PurePrep: Guardar recetas
```

#### Alternativa B — 26 / 30

```
PurePrep: Libro de recetas
```

"Recetario" is the natural word for a personal recipe book in Spain; "guardar recetas" leads the
short description. No "sin anuncios" or "gratis" in the title.

### Descripción breve — 78 / 80

```
Guarda recetas de cualquier web: ingredientes y pasos. Sin anuncios ni cuenta.
```

### Descripción completa — 3,961 / 4,000

```
La receta, sin ruido.

PurePrep es un recetario en el móvil para quien solo quiere cocinar. Comparte un enlace desde cualquier web y PurePrep guarda solo lo que necesitas para cocinar: los ingredientes, los pasos, los temporizadores y una foto. Sin anuncios, sin ventanas emergentes, sin vídeos que se reproducen solos, sin diez párrafos de historia personal.

Sin anuncios. Sin cuenta. Sin rastreo.

GUARDA RECETAS DE CUALQUIER WEB
• Comparte para importar: toca «Compartir» en el navegador o en cualquier app que comparta enlaces y elige PurePrep.
• ¿Has copiado un enlace? Abre PurePrep y te ofrecerá importarlo.
• Busca en la web sin salir de la app y toca «Añadir a PurePrep» en la página de la receta.
• Haz una foto a una página de un libro de cocina o a una captura de pantalla, o pega el texto de una receta desde una nota o un mensaje.
• Mucho más que guardar un enlace: PurePrep entiende las raciones, los tiempos de preparación y cocción, los temporizadores con nombre como «Sofríe la cebolla · 10–12 min» y qué ingredientes usa cada paso.
• ¿La página no tiene foto? Recibes una imagen del plato creada con IA en lugar de una tarjeta vacía.

COCINA PASO A PASO
• El modo Concentración muestra un paso cada vez, siempre ajustado a la pantalla, con «Necesitarás» y los ingredientes de ese paso.
• Los temporizadores de cocina están dentro de los pasos: toca uno y empieza la cuenta atrás. Puedes tener varios a la vez, y una barra de temporizadores te acompaña por toda la app.
• Escucha el paso en voz alta (y detenlo cuando quieras) o avanza por la receta con comandos de voz, sin tocar la pantalla.
• La pantalla no se apaga, aunque tengas las manos llenas de harina.

AJUSTA, CONVIERTE, ORGANIZA
• Ajusta las raciones por personas: Para − 4 + personas, y todas las cantidades se recalculan, en las fracciones que escribiría un cocinero.
• Cambia cualquier receta entre unidades métricas y estadounidenses.
• Un organizador de recetas que no estorba: favoritas, «Por cocinar» / «Cocinadas», tus propias notas y orden por añadidas recientemente, cocinadas recientemente o de la A a la Z.
• Edítalo todo: una fila por ingrediente y por paso, arrastra para reordenar, añade tu propia foto.
• Escribe a mano las recetas de la familia.
• Traduce una receta a tu idioma. PurePrep habla español, inglés, polaco, alemán, francés, italiano y neerlandés.
• Tema oscuro y claro.

TUYAS, TAMBIÉN SIN CONEXIÓN
• Tu recetario vive en tu móvil. Las recetas guardadas se abren sin internet: en la cocina, de vacaciones, en un avión.
• Haz una copia de seguridad de toda tu colección, fotos incluidas, en un solo archivo y restáurala en un móvil nuevo.

SIN ANUNCIOS. SIN CUENTA. SIN RASTREO.
• Sin registro, sin correo electrónico, sin contraseña.
• Sin anuncios y sin SDK de analítica o rastreo de terceros.
• Tus recetas, notas y fotos se quedan en tu dispositivo. Al importar solo se envía lo que eliges importar, para que lo lea la IA. La receta limpia vuelve y se guarda en tu móvil.
• La búsqueda web abre Google y las webs de recetas tal cual, con sus propias cookies. PurePrep no añade ningún rastreo propio.

QUÉ ES GRATIS
Guardar, editar, ajustar raciones, los temporizadores, el modo Concentración y añadir recetas a mano son gratis, sin límite de recetas. Importar y traducir usan IA que funciona en servidores de pago, así que gastan Smart Credits: 1 por un enlace o un texto pegado, 2 por una foto y 1 por traducir una receta a un idioma nuevo (la traducción se guarda, así que solo pagas una vez). Empiezas con 10 Smart Credits gratis. Después, los créditos se compran en pequeños paquetes dentro de la app. Sin suscripción.

POR QUÉ LA HICE
Quería tener mis recetas en un sitio tranquilo, sin pasar anuncios e historias personales solo para saber cuánta harina lleva. Así que hice PurePrep, y cocino con ella todos los días. Si algo no te funciona, escríbeme: andrzej@lechdigital.nl

Política de privacidad: https://pureprep.lechdigital.nl/privacy
```

---

## 6. Italian (it-IT) → Store listing → App details

Written in Italian, not translated from the English. The UI terms match `AppResources.it.resx`:
modalità Focus, Ti serve, Da cucinare / Cucinate, Aggiungi a PurePrep, Per − 4 + persone, Smart
Credits. The app says "tu", so the listing does too. Search words: *ricettario*, *salvare ricette*,
*app ricette*, *ricette di cucina*.

### Nome dell'app — 30 / 30

```
PurePrep: Ricettario di cucina
```

#### Alternativa A — 30 / 30

```
PurePrep: Salva le tue ricette
```

#### Alternativa B — 25 / 30

```
PurePrep: Ricette e timer
```

"Ricettario" is how Italians name a personal recipe collection and what they search for; "salva
ricette" leads the short description. No "senza pubblicità" or "gratis" in the title.

### Breve descrizione — 79 / 80

```
Salva ricette da qualsiasi sito: solo ingredienti e passaggi. Senza pubblicità.
```

### Descrizione completa — 3,984 / 4,000

```
La ricetta, senza rumore.

PurePrep è un ricettario sul telefono per chi vuole semplicemente cucinare. Condividi un link da qualsiasi sito e PurePrep salva solo quello che serve ai fornelli: ingredienti, passaggi, timer e una foto. Niente pubblicità, niente pop-up, niente video che partono da soli, niente racconti di dieci paragrafi prima della ricetta.

Niente pubblicità. Niente account. Niente tracciamento.

SALVA RICETTE DA QUALSIASI SITO
• Condividi per importare: nel browser o in qualsiasi app che condivide link, tocca «Condividi» e scegli PurePrep.
• Hai copiato un link? Apri PurePrep e ti proporrà di importarlo.
• Cerca sul web senza uscire dall'app, poi tocca «Aggiungi a PurePrep» sulla pagina della ricetta.
• Fotografa la pagina di un libro di cucina o uno screenshot, oppure incolla il testo di una ricetta da una nota o da un messaggio.
• Molto più di un segnalibro: PurePrep riconosce porzioni, tempi di preparazione e di cottura, timer con nome come «Soffriggi la cipolla · 10–12 min» e quali ingredienti servono in ogni passaggio.
• La pagina non ha una foto? Ricevi un'immagine del piatto creata con l'IA invece di una scheda vuota.

CUCINA PASSO DOPO PASSO
• La modalità Focus mostra un passaggio alla volta, sempre adattato allo schermo, con «Ti serve» e gli ingredienti di quel passaggio.
• I timer da cucina sono dentro i passaggi: toccane uno per avviarlo. Puoi farne andare diversi insieme, e una barra dei timer ti segue in tutta l'app.
• Fatti leggere il passaggio ad alta voce (e fermalo quando vuoi) oppure scorri la ricetta a mani libere con i comandi vocali.
• Lo schermo resta acceso, anche con le mani piene di farina.

SCALA, CONVERTI, ORGANIZZA
• Scala le dosi per persone: Per − 4 + persone, e tutte le quantità si aggiornano, nelle frazioni che scriverebbe un cuoco.
• Passa qualsiasi ricetta dalle unità metriche a quelle americane e viceversa.
• Un ricettario in ordine senza fatica: preferite, «Da cucinare» / «Cucinate», le tue note e ordinamento per aggiunte di recente, cucinate di recente o dalla A alla Z.
• Modifica tutto: una riga per ogni ingrediente e passaggio, trascina per riordinare, aggiungi la tua foto.
• Scrivi a mano le ricette di famiglia.
• Traduci una ricetta nella tua lingua. PurePrep parla italiano, inglese, polacco, tedesco, francese, spagnolo e olandese.
• Tema scuro e chiaro.

TUE, ANCHE OFFLINE
• Le tue ricette vivono sul tuo telefono. Quelle salvate si aprono senza internet: in cucina, in vacanza, in aereo.
• Fai il backup di tutta la raccolta, foto comprese, in un unico file e ripristinala su un nuovo telefono.

NIENTE PUBBLICITÀ. NIENTE ACCOUNT. NIENTE TRACCIAMENTO.
• Nessuna registrazione, nessuna email, nessuna password.
• Nessuna pubblicità e nessun SDK di analisi o tracciamento di terze parti.
• Ricette, note e foto restano sul tuo dispositivo. Un'importazione invia solo ciò che hai scelto di importare, perché lo legga l'IA. La ricetta pulita torna indietro e viene salvata sul tuo telefono.
• La ricerca web apre Google e i siti di ricette così come sono, con i loro cookie. PurePrep non aggiunge alcun tracciamento proprio.

COSA È GRATIS
Salvare, modificare, scalare le dosi, i timer, la modalità Focus e aggiungere ricette a mano sono gratis, senza limiti al numero di ricette. Importazione e traduzione usano un'IA che gira su server a pagamento, quindi consumano Smart Credits: 1 per un link o un testo incollato, 2 per una foto e 1 per tradurre una ricetta in una nuova lingua (la traduzione resta salvata, quindi la paghi una volta sola). Inizi con 10 Smart Credits gratuiti. Poi i crediti si acquistano in piccoli pacchetti nell'app. Nessun abbonamento.

PERCHÉ L'HO CREATA
Volevo le mie ricette in un posto tranquillo, senza scorrere pubblicità e storie di vita solo per scoprire quanta farina serve. Così ho creato PurePrep, e ci cucino ogni giorno. Se qualcosa non ti funziona, scrivimi: andrzej@lechdigital.nl

Informativa sulla privacy: https://pureprep.lechdigital.nl/privacy
```

---

## 7. Dutch (nl-NL) → Store listing → App details

Written in Dutch, not translated from the English. The UI terms match `AppResources.nl.resx`:
Focusmodus, Je hebt nodig, Wil ik koken / Gekookt, Toevoegen aan PurePrep, Voor − 4 + personen,
Smart Credits. The app says "je", so the listing does too. Search words: *recepten opslaan*,
*receptenapp*, *kookboek*, *recepten bewaren*.

### App-naam — 26 / 30

```
PurePrep: Recepten opslaan
```

#### Alternatief A — 29 / 30

```
PurePrep: Receptenapp & timer
```

#### Alternatief B — 29 / 30

```
PurePrep: Kookboek & recepten
```

"Recepten opslaan" is the exact phrase Dutch users search for this kind of app; "receptenapp" and
"kookboek" are in the full description. No "zonder reclame" or "gratis" in the title.

### Korte beschrijving — 78 / 80

```
Recepten opslaan van elke site – alleen ingrediënten en stappen. Geen reclame.
```

### Volledige beschrijving — 3,835 / 4,000

```
Het recept, zonder ruis.

PurePrep is een receptenapp voor wie gewoon wil koken. Deel een link van een willekeurige website en PurePrep bewaart alleen waar je mee kookt: de ingrediënten, de stappen, de timers en een foto. Geen reclame, geen pop-ups, geen video's die vanzelf starten, geen levensverhaal van tien alinea's.

Geen reclame. Geen account. Geen tracking.

RECEPTEN OPSLAAN VAN ELKE WEBSITE
• Delen om te importeren: tik in je browser of een andere app die links deelt op “Delen” en kies PurePrep.
• Link gekopieerd? Open PurePrep en de app biedt aan hem te importeren.
• Zoek op het web zonder de app te verlaten en tik op de receptpagina op “Toevoegen aan PurePrep”.
• Maak een foto van een pagina uit een kookboek of van een screenshot, of plak recepttekst uit een notitie of bericht.
• Meer dan een bladwijzer: PurePrep herkent porties, bereidings- en kooktijd, timers met een naam zoals “Ui fruiten · 10–12 min” en welke ingrediënten elke stap gebruikt.
• Geen foto op de pagina? Dan krijg je een door AI gemaakte afbeelding van het gerecht in plaats van een lege kaart.

STAP VOOR STAP KOKEN
• Focusmodus toont één stap tegelijk, altijd passend op het scherm, met onder “Je hebt nodig” de ingrediënten voor die stap.
• Kookwekkers zitten in de stappen: tik erop en de timer loopt. Er kunnen er meerdere tegelijk lopen, en een timerbalk volgt je door de hele app.
• Laat de stap voorlezen (en stop wanneer je wilt) of ga handsfree door het recept met spraakopdrachten.
• Het scherm blijft aan, ook met handen vol meel.

SCHALEN, OMREKENEN, ORDENEN
• Schaal op aantal personen: Voor − 4 + personen, en alle hoeveelheden rekenen mee, in breuken zoals een kok ze zou schrijven.
• Zet elk recept om tussen metrische en Amerikaanse eenheden.
• Een receptenverzameling die je niet in de weg zit: favorieten, “Wil ik koken” / “Gekookt”, je eigen notities en sorteren op recent toegevoegd, recent gekookt of A–Z.
• Bewerk alles: één regel per ingrediënt en stap, sleep om de volgorde te wijzigen, voeg je eigen foto toe.
• Typ familierecepten gewoon zelf in.
• Vertaal een recept naar je eigen taal. PurePrep spreekt Nederlands, Engels, Pools, Duits, Frans, Spaans en Italiaans.
• Donker en licht thema.

VAN JOU, OOK OFFLINE
• Je digitale kookboek staat op je telefoon. Opgeslagen recepten openen zonder internet: in de keuken, op vakantie, in het vliegtuig.
• Maak een back-up van je hele verzameling, inclusief foto's, in één bestand en zet die terug op een nieuwe telefoon.

GEEN RECLAME. GEEN ACCOUNT. GEEN TRACKING.
• Geen registratie, geen e-mail, geen wachtwoord.
• Geen reclame en geen analyse- of tracking-SDK's van derden.
• Je recepten, notities en foto's blijven op je apparaat. Een import stuurt alleen wat je zelf kiest om te importeren, zodat AI het kan lezen. Het nette recept komt terug en wordt op je telefoon opgeslagen.
• Zoeken op het web opent Google en receptensites zoals ze zijn, met hun eigen cookies. PurePrep voegt zelf geen tracking toe.

WAT IS GRATIS
Opslaan, bewerken, schalen, timers, Focusmodus en zelf recepten invoeren zijn gratis, zonder limiet op het aantal recepten. Importeren en vertalen gebruiken AI die op betaalde servers draait, dus daarvoor gebruik je Smart Credits: 1 voor een link of geplakte tekst, 2 voor een foto en 1 om een recept naar een nieuwe taal te vertalen (de vertaling blijft bewaard, dus je betaalt maar één keer). Je begint met 10 gratis Smart Credits. Daarna koop je credits in kleine pakketten in de app. Geen abonnement.

WAAROM IK HET HEB GEMAAKT
Ik wilde mijn recepten op één rustige plek, zonder langs advertenties en levensverhalen te scrollen om te zien hoeveel bloem erin moet. Dus heb ik PurePrep gemaakt, en ik kook er zelf elke dag mee. Werkt er iets niet voor je? Mail me: andrzej@lechdigital.nl

Privacybeleid: https://pureprep.lechdigital.nl/privacy
```

---

## 8. Graphics

| Slot | File | Size | Status |
|------|------|------|--------|
| App icon | `store-assets/icon-512.png` | 512 × 512, 32-bit | Unchanged. The options are in `icon-options/` and are proposals only |
| Feature graphic | `store-assets/feature-1024x500.new.png` | 1024 × 500 | **New**. Rename it over `feature-1024x500.png` once you approve it |
| Phone screenshots 1–8 | `store-assets/phone/…` | 1080 × 1920 | **Not captured yet.** Plan below |

The feature graphic carries the positioning in one glance. The headline is "The recipe. Without
the **noise.**" (the landing page's line, with one accent word). The three pills say "No ads · No
account · No tracking", and the pierogi from the app's own sample recipe break out of the photo card.
There is no fake UI in it.

## 9. Phone screenshot slot plan (8)

Every slot is a headline over a **real capture** from the final 1.4 build on the Pixel. The
headline has two lines, and the word in *italics* is the lime accent. Each slot also has a smaller
subline. Captures are cropped to remove Android's status and navigation bars, as in the reference
pipeline. The library should contain the sample Pierogi plus a couple of good-looking recipes with
photos, added by hand so they cost no credits. Record in the dark theme to match the feature
graphic.

The order follows what a stranger needs to decide. First the result, then how a recipe gets in,
then how you cook from it, then what makes it trustworthy.

| # | Headline (accent in *italics*) | Subline | Screen to capture |
|---|---|---|---|
| 1 | The recipe. / Without the *noise.* | Just ingredients and steps. No ads, no life story. | **Recipe detail**, Pierogi: photo header, title, meta line, "Serves − 4 +", first ingredients |
| 2 | *Share* a link. / Get the recipe. | From your browser or any app. One tap to import. | **Import sheet** over the app after sharing a link from Chrome: domain, "Uses 1 Smart Credit · you have N", Import |
| 3 | *Search* the web. / Add in one tap. | Find a recipe without leaving PurePrep. | **In-app web search** on a recipe page, with the floating "Add to PurePrep" button visible |
| 4 | Cook *step* by step. | Every step fits the screen, with what you'll need. | **Focus Mode**: a mid-recipe step with the "You'll need" chips and a timer chip |
| 5 | Timers that *know* / the recipe. | "Fry onion · 10–12 min". Tap to start. Run several. | **Focus Mode with timers running**: the running-timers bar showing two named timers |
| 6 | *Scale* it for / any table. | Serves − 8 +. Every amount follows. | **Recipe detail** after changing servings (the sample serves 6; captured at 8), with the reset icon and scaled fractions visible |
| 7 | Your recipe box, / *organized.* | Favorites, Want to cook, Cooked, notes. Offline. | **Library** with photos, the filter chips (All · Want to cook · Cooked · Favorites) and a ♥ on one card |
| 8 | No ads. No account. / No *tracking.* | Your recipes stay on your phone. | **Add sheet**: Paste a link · From a photo · From text · type it in yourself (shows the free manual path) |

Optional swaps if a capture disappoints:
- 8 ↔ **Editor** (one row per ingredient, drag handles, photo), headline "Edit *anything.*".
- 7 ↔ **Settings → language list**, headline "Cooks in *seven* / languages."

### Localized headlines

Same eight slots, same captures (recorded in each language's UI), same rule: two lines split at
"/", the *italic* word is the lime accent. The UI words in the sublines match each app's resx.

#### de-DE

| # | Headline | Subline |
|---|---|---|
| 1 | Das Rezept. / Ohne das *Drumherum.* | Nur Zutaten und Schritte. Keine Werbung, keine Lebensgeschichte. |
| 2 | Link *teilen*. / Rezept haben. | Aus dem Browser oder jeder App. Ein Tipp zum Import. |
| 3 | Im Web *suchen*. / Mit einem Tipp speichern. | Rezepte finden, ohne PurePrep zu verlassen. |
| 4 | Schritt für / *Schritt* kochen. | Jeder Schritt passt auf den Bildschirm, mit allem, was du brauchst. |
| 5 | Timer, die das / Rezept *kennen*. | „Zwiebel anbraten · 10–12 Min.“ Antippen, fertig. Mehrere gleichzeitig. |
| 6 | Für jeden Tisch / *skaliert*. | Für − 8 + Personen. Alle Mengen ziehen mit. |
| 7 | Dein Kochbuch, / *geordnet*. | Favoriten, Möchte ich kochen, Gekocht, Notizen. Offline. |
| 8 | Keine Werbung. Kein Konto. / Kein *Tracking*. | Deine Rezepte bleiben auf deinem Handy. |

#### fr-FR

| # | Headline | Subline |
|---|---|---|
| 1 | La recette. / Sans le *bruit.* | Juste les ingrédients et les étapes. Sans pub, sans roman. |
| 2 | *Partagez* un lien. / Voilà la recette. | Depuis le navigateur ou n'importe quelle appli. Import en un geste. |
| 3 | *Cherchez* sur le web. / Ajoutez d'un geste. | Trouvez une recette sans quitter PurePrep. |
| 4 | Cuisinez *étape* / par étape. | Chaque étape tient à l'écran, avec ce qu'il vous faut. |
| 5 | Des minuteurs qui / *connaissent* la recette. | « Faire revenir l'oignon · 10–12 min ». Touchez, c'est parti. Plusieurs à la fois. |
| 6 | *Adaptée* à / chaque tablée. | Pour − 8 + personnes. Chaque quantité suit. |
| 7 | Votre carnet de recettes, / *rangé.* | Favoris, À cuisiner, Cuisinées, notes. Hors ligne. |
| 8 | Sans pub. Sans compte. / Sans *pistage.* | Vos recettes restent sur votre téléphone. |

#### es-ES

| # | Headline | Subline |
|---|---|---|
| 1 | La receta. / Sin *ruido.* | Solo ingredientes y pasos. Sin anuncios ni historias. |
| 2 | *Comparte* un enlace. / Ten la receta. | Desde el navegador o cualquier app. Un toque para importar. |
| 3 | *Busca* en la web. / Añade con un toque. | Encuentra recetas sin salir de PurePrep. |
| 4 | Cocina *paso* / a paso. | Cada paso cabe en la pantalla, con lo que necesitarás. |
| 5 | Temporizadores que / *conocen* la receta. | «Sofríe la cebolla · 10–12 min». Toca y listo. Varios a la vez. |
| 6 | Raciones *a medida* / para tu mesa. | Para − 8 + personas. Todas las cantidades se ajustan. |
| 7 | Tu recetario, / *ordenado.* | Favoritas, Por cocinar, Cocinadas, notas. Sin conexión. |
| 8 | Sin anuncios. Sin cuenta. / Sin *rastreo.* | Tus recetas se quedan en tu móvil. |

#### it-IT

| # | Headline | Subline |
|---|---|---|
| 1 | La ricetta. / Senza *rumore.* | Solo ingredienti e passaggi. Niente pubblicità, niente racconti. |
| 2 | *Condividi* un link. / Ecco la ricetta. | Dal browser o da qualsiasi app. Un tocco per importare. |
| 3 | *Cerca* sul web. / Aggiungi con un tocco. | Trova una ricetta senza uscire da PurePrep. |
| 4 | Cucina *passo* / dopo passo. | Ogni passaggio sta nello schermo, con ciò che ti serve. |
| 5 | Timer che *conoscono* / la ricetta. | «Soffriggi la cipolla · 10–12 min». Tocca e parte. Anche più insieme. |
| 6 | Dosi *su misura* / per ogni tavola. | Per − 8 + persone. Tutte le quantità si adeguano. |
| 7 | Il tuo ricettario, / *in ordine.* | Preferite, Da cucinare, Cucinate, note. Offline. |
| 8 | Niente pubblicità. / Niente account, niente *tracciamento.* | Le tue ricette restano sul tuo telefono. |

#### nl-NL

| # | Headline | Subline |
|---|---|---|
| 1 | Het recept. / Zonder *ruis.* | Alleen ingrediënten en stappen. Geen reclame, geen levensverhaal. |
| 2 | *Deel* een link. / Klaar is je recept. | Vanuit je browser of elke app. Eén tik om te importeren. |
| 3 | *Zoek* op het web. / Voeg toe met één tik. | Vind een recept zonder PurePrep te verlaten. |
| 4 | Kook *stap* / voor stap. | Elke stap past op het scherm, met wat je nodig hebt. |
| 5 | Timers die het / recept *kennen*. | “Ui fruiten · 10–12 min”. Tik en hij loopt. Meerdere tegelijk. |
| 6 | *Geschaald* voor / elke tafel. | Voor − 8 + personen. Alle hoeveelheden rekenen mee. |
| 7 | Je kookboek, / *op orde.* | Favorieten, Wil ik koken, Gekookt, notities. Offline. |
| 8 | Geen reclame. Geen account. / Geen *tracking.* | Je recepten blijven op je telefoon. |

---

## Claims checked against the code (1.4.0)

Every claim above was checked in the repo. Nothing was carried over from the old listing.

| Claim | Where it is true |
|---|---|
| No account, no ads, no third-party analytics/tracking SDKs | `src/PurePrep.Server/wwwroot/privacy.html` ("What we do not do") |
| Recipes, notes and photos stay on the device | privacy.html; notes "never sent to the server" (1.4 design §6) |
| Share to import, clipboard offer | `MainActivity` SEND `text/plain` intent filter; 1.4 design §4 |
| In-app web search, "Add to PurePrep", no credit for browsing | privacy.html "Searching for recipes on the web"; `AddToPurePrep` string |
| Photo import (2 credits), text import (1), link (1) | `CreditOptions.CostPerImageParse = 2`, `CostPerParse = 1`; `ImportPhotoHint` |
| Translation 1 credit, kept, paid once | `CostPerTranslation = 1`; `TranslateCostFormat` |
| 10 free Smart Credits to start | `CreditOptions.FreeCredits = 10`, not overridden in appsettings.json |
| No subscription | only consumable packs `credits_10/20/50/150` |
| No recipe limit | there is no cap in code. The old listing's "up to 10 saved recipes" is gone |
| AI picture when the page has no photo | `ImageEndpoint`, 1.4 design §5.6 (included in the import credit) |
| 7 languages; voice commands; read aloud with Stop; screen stays awake | PRODUCT.md; `VoiceCommandsTitle`, `ReadStep`, `KeepScreenAwake` |
| Fractions "like a cook writes them" | commits 15b0202 / 1aefc8b |
