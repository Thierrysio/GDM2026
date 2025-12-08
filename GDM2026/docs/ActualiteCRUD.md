# Endpoints CRUD Actualité

L'API Symfony expose plusieurs routes pour manipuler les actualités publiées sur Dantec Market.

## Routes disponibles

- **GET** `/api/crud/actualite/list` — renvoie la liste complète des actualités (`data` = tableau d'objets sérialisés).
- **POST** `/api/crud/actualite/get` — charge une actualité spécifique.
  - Corps JSON : `{ "id": 12 }`
- **POST** `/api/crud/actualite/create` — crée une nouvelle actualité.
  - Corps JSON : champs scalaires + associations (par exemple titre, description, image, dates liées, etc.).
- **POST** `/api/crud/actualite/update` — met à jour une actualité existante.
  - Corps JSON : `{ "id": 12, ...champs à modifier... }`
- **POST** `/api/crud/actualite/delete` — supprime une actualité.
  - Corps JSON : `{ "id": 12 }`

## Exemple d'appel `create`

Le corps doit contenir les champs attendus par le backend (par exemple titre, description ou URL d'image). Exemple minimal avec des clés génériques :

```json
{
  "titre": "Nouvelle fonctionnalité",
  "description": "Annonce rapide publiée depuis l'application mobile",
  "image": "/images/ma-vignette.png"
}
```

Le serveur répond avec un statut HTTP indiquant le succès ou, en cas d'erreur de validation, un code 4xx décrivant le problème. Les clients existants peuvent utiliser `Apis.PostBoolAsync` pour poster la charge utile sérialisée en JSON.
