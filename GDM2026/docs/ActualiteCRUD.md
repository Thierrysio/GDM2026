# Endpoint de création d'actualité

L'API Symfony expose une route dédiée pour enregistrer une nouvelle actualité.

- **Méthode :** `POST`
- **Route :** `/api/crud/actualite/create`
- **Effet :** crée un enregistrement d'actualité côté serveur.

## Exemple d'appel JSON

Le corps doit contenir les champs attendus par le backend (par exemple titre, description ou URL d'image). Exemple minimal avec des clés génériques :

```json
{
  "titre": "Nouvelle fonctionnalité",
  "description": "Annonce rapide publiée depuis l'application mobile",
  "image": "/images/ma-vignette.png"
}
```

Le serveur répondra avec un statut HTTP indiquant le succès ou, en cas d'erreur de validation, un code 4xx décrivant le problème. Les clients existants peuvent utiliser `Apis.PostBoolAsync` pour poster la charge utile sérialisée en JSON.
