BijouxElegance 💎

## Description
**BijouxElegance** est une application web e-commerce spécialisée dans la vente de bijoux élégants.  
Le projet inclut un **chatbot intelligent** basé sur **RAG (Retrieval-Augmented Generation)**, permettant aux utilisateurs de poser des questions sur les produits et le panier, avec des réponses fiables issues uniquement de la base de données.

Le projet est conçu pour offrir une expérience utilisateur fluide et moderne, avec un focus sur :  
- Consultation du catalogue produits.  
- Suggestions intelligentes de bijoux.  
- Gestion du panier et disponibilité en temps réel.  
- Assistance via chatbot sans hallucination d’informations.

---

## Fonctionnalités

### 1. Catalogue de bijoux
- Affichage des produits avec image, nom, description, prix et disponibilité.  
- Gestion du stock :  
  - `Stock = 0` → “Non disponible”  
  - `Stock < 3` → “Bientôt saturé”  
  - `Stock ≥ 3` → “Disponible”  

### 2. Panier
- Ajout et suppression de produits.  
- Quantité limitée selon le stock.  
- Calcul automatique du total.  

### 3. Chatbot RAG
- Répond uniquement à partir des données du catalogue et du panier.  
- Propose 3 à 4 suggestions de produits lorsque demandé.  
- Ne fournit pas l’ID des produits pour l’utilisateur.  
- Capable de guider l’utilisateur sur les disponibilités et le panier.  

### 4. Interface utilisateur
- Design moderne et responsive.  
- Navigation intuitive entre le catalogue, le panier et le chatbot.  

---

## Technologies utilisées
- **Back-end** : ASP.NET Core / Razor Pages  
- **Front-end** : HTML, CSS, JavaScript  
- **Base de données** : SQL Server / MySQL  
- **Chatbot** : LLM + Vector Database pour RAG  
- **Gestion des dépendances** : NuGet, npm  

---

## Installation

1. Cloner le dépôt :  
```bash
git clone https://github.com/votre-utilisateur/BijouxElegance.git
cd BijouxElegance
````

2. Installer les dépendances :

```bash
# Front-end
npm install

# Back-end
dotnet restore
```

3. Configurer la base de données dans `appsettings.json`.

4. Lancer le projet :

```bash
# Back-end
dotnet run

# Front-end (si séparé)
npm start
```

---

## Utilisation

* Accéder à l’application via le navigateur à [http://localhost:5000](http://localhost:5000).
* Naviguer dans le catalogue et ajouter des produits au panier.
* Interagir avec le chatbot pour obtenir des informations sur les produits, la disponibilité ou des suggestions.

---

## Contribution

1. Fork le projet.
2. Crée une branche pour tes modifications :

```bash
git checkout -b feature/nom-de-la-fonctionnalité
```

3. Commit tes changements :

```bash
git commit -m "Description des changements"
```

4. Push et crée une Pull Request vers `main`.

---

## Auteur

**Salwa Zagri** – Développeuse
Projet réalisé dans le cadre d’un projet académique/professionnel.

