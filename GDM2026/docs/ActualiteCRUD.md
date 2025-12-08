# CRUD Actualité

L'API Symfony expose plusieurs routes pour gérer les actualités. Toutes renvoient un JSON sérialisé ; côté mobile, on utilise le
client `Apis` pour orchestrer les appels.

## Récupérer la liste

- **Méthode :** `GET`
- **Route :** `/api/crud/actualite/list`
- **Effet :** renvoie un tableau complet d'actualités sérialisées dans la propriété `data`.

## Charger un enregistrement

- **Méthode :** `POST`
- **Route :** `/api/crud/actualite/get`
- **Corps JSON :** `{ "id": 12 }`
- **Effet :** retourne l'actualité demandée ou une erreur si l'identifiant est inconnu.

## Créer une actualité

- **Méthode :** `POST`
- **Route :** `/api/crud/actualite/create`
- **Effet :** crée un enregistrement d'actualité côté serveur.

### Exemple d'appel JSON

Le corps doit contenir les champs attendus par le backend (par exemple titre, description ou URL d'image). Exemple minimal avec
des clés génériques :

```json
{
  "titre": "Nouvelle fonctionnalité",
  "description": "Annonce rapide publiée depuis l'application mobile",
  "image": "/images/ma-vignette.png"
}
```

Le serveur répondra avec un statut HTTP indiquant le succès ou, en cas d'erreur de validation, un code 4xx décrivant le problème.
Les clients existants peuvent utiliser `Apis.PostBoolAsync` pour poster la charge utile sérialisée en JSON.

## Mettre à jour une actualité

- **Méthode :** `POST`
- **Route :** `/api/crud/actualite/update`
- **Corps JSON :** `{ "id": 12, ...champs à modifier... }`
- **Effet :** applique les modifications fournies pour l'actualité ciblée.

## Supprimer une actualité

- **Méthode :** `POST`
- **Route :** `/api/crud/actualite/delete`
- **Corps JSON :** `{ "id": 12 }`
- **Effet :** supprime l'enregistrement correspondant.
