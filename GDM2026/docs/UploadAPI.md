# API d'upload Symfony

L'endpoint `/api/mobile/upload` du backend Symfony exige que l'utilisateur soit authentifié avant tout envoi de fichier. Sans session (cookie `PHPSESSID`/`REMEMBERME`) ou en-tête d'authentification approprié, le serveur renvoie `401 Unauthorized` comme le montre le contrôleur :

```php
if (null === $this->getUser()) {
    return new JsonResponse(
        ['error' => 'Authentification requise'],
        Response::HTTP_UNAUTHORIZED
    );
}
```

Pour réussir l'appel depuis l'application :

- Ouvrir une session au préalable (par exemple via la route de login) afin que le `HttpClient` réutilise les cookies partagés (`AppHttpClientFactory` conserve le conteneur de cookies commun aux appels protégés comme l'upload). L'écran d'upload recharge aussi le token mémorisé (`SessionService`) et le renvoie en en-tête `Authorization: Bearer ...` via `ImageUploadService`.
- Poster un formulaire multipart contenant au moins le champ `file` (nom du fichier inclus). La clé `folder` optionnelle envoyée par le client est sans effet dans le contrôleur actuel, qui stocke toujours les fichiers sous `/public/images`.
- En cas de succès, la réponse JSON contient une URL relative (`/images/<fichier>`).

Si un `401` survient malgré tout :

- vérifiez dans les préférences que le token d'authentification n'est pas vide (l'appli l'ajoute automatiquement à l'en-tête `Authorization` si présent) ;
- assurez-vous que la session backend est encore valide (le conteneur de cookies commun peut avoir expiré après une longue inactivité) ;
- en mode debug, le message d'erreur inclura désormais si un en-tête `Authorization` a bien été envoyé et quelle BaseAddress a été utilisée, ce qui aide à cibler les problèmes d'identifiants manquants ou d'URL mal configurée.
- l'application nettoie automatiquement la session locale si le serveur renvoie un `401` pendant l'envoi ; reconnectez-vous depuis l'écran d'upload pour rafraîchir le jeton et les cookies avant de réessayer.
