# Documentation API - Fonctionnalité Fidélité

**Application** : GDM2026 - Dantec Market  
**Version** : 1.0  
**Date** : Janvier 2025

---

## Table des matières

1. [Vue d'ensemble](#vue-densemble)
2. [GET /api/mobile/getNombreCommandes](#1-get-apimobilegetnombrecommandes)
3. [POST /api/mobile/getLoyaltyByQrCode](#2-post-apimobilegetloyaltybyqrcode)
4. [POST /api/mobile/applyLoyaltyReduction](#3-post-apimobileapplyloyaltyreduction)
5. [Modifications base de données](#modifications-base-de-données)
6. [Entités Doctrine](#entités-doctrine)
7. [Correspondance des champs](#correspondance-des-champs)
8. [Tests recommandés](#tests-recommandés)

---

## Vue d'ensemble

### Objectif
Permettre aux administrateurs d'appliquer des réductions fidélité sur les commandes des clients en scannant leur QR code.

### Règle de conversion
| Points fidélité | Valeur en euros |
|-----------------|-----------------|
| 1 point | 0.01 € |
| 10 points | 0.10 € |
| 100 points | 1.00 € |

### Flux utilisateur
1. L'admin ouvre une commande/réservation
2. L'admin clique sur le bouton "Fidélité"
3. L'admin scanne le QR code du client
4. L'app affiche les points disponibles du client
5. L'admin choisit combien de points utiliser
6. La réduction est appliquée et les points sont déduits

---

## 1. GET /api/mobile/getNombreCommandes

### Description
Retourne le nombre de commandes/réservations groupées par état.

### Authentification
? **Requise** (Bearer Token)

### Requête
```http
GET /api/mobile/getNombreCommandes
Authorization: Bearer <token>
```

### Réponse - Succès (200)
```json
{
    "Confirmée": 12,
    "En cours de traitement": 5,
    "Traitée": 8,
    "Livrée": 45,
    "A confirmer": 3
}
```

### Implémentation Symfony
```php
#[Route('/api/mobile/getNombreCommandes', name: 'get_nombre_commandes', methods: ['GET'])]
public function getNombreCommandes(): JsonResponse
{
    $stats = $this->commandeRepository->createQueryBuilder('c')
        ->select('c.etat, COUNT(c.id) as total')
        ->groupBy('c.etat')
        ->getQuery()
        ->getResult();

    $result = [];
    foreach ($stats as $stat) {
        $result[$stat['etat']] = (int) $stat['total'];
    }

    return $this->json($result);
}
```

---

## 2. POST /api/mobile/getLoyaltyByQrCode

### Description
Récupère les informations de fidélité d'un client à partir du contenu de son QR code.

### Authentification
? **Requise** (Bearer Token)

### Requête
```http
POST /api/mobile/getLoyaltyByQrCode
Authorization: Bearer <token>
Content-Type: application/json
```

### Corps de la requête
```json
{
    "qrCode": "string"
}
```

| Paramètre | Type | Obligatoire | Description |
|-----------|------|-------------|-------------|
| `qrCode` | string | ? | Contenu du QR code scanné |

### Réponse - Succès (200) - Client trouvé
```json
{
    "success": true,
    "message": "Client trouvé",
    "data": {
        "userId": 123,
        "nom": "Dupont",
        "prenom": "Jean",
        "email": "jean.dupont@email.com",
        "couronnes": 150
    }
}
```

> ?? **Note** : Le champ JSON s'appelle `couronnes` pour l'app mobile, mais côté Symfony vous utilisez `fidelite`.

### Réponse - Échec (200) - Client non trouvé
```json
{
    "success": false,
    "message": "QR code invalide ou client introuvable",
    "data": null
}
```

### Réponse - Échec (200) - QR code manquant
```json
{
    "success": false,
    "message": "QR code manquant",
    "data": null
}
```

### Implémentation Symfony
```php
#[Route('/api/mobile/getLoyaltyByQrCode', name: 'get_loyalty_by_qrcode', methods: ['POST'])]
public function getLoyaltyByQrCode(Request $request): JsonResponse
{
    // 1. Décoder le JSON
    $data = json_decode($request->getContent(), true);
    $qrCode = $data['qrCode'] ?? null;

    // 2. Valider le paramètre
    if (empty($qrCode)) {
        return $this->json([
            'success' => false,
            'message' => 'QR code manquant',
            'data' => null
        ]);
    }

    // 3. Chercher l'utilisateur par son QR code
    // Option A : Le QR code contient l'ID utilisateur
    $user = $this->userRepository->find($qrCode);
    
    // Option B : Le QR code contient un token unique
    // $user = $this->userRepository->findOneBy(['qrToken' => $qrCode]);
    
    // Option C : Le QR code contient l'email
    // $user = $this->userRepository->findOneBy(['email' => $qrCode]);

    // 4. Vérifier si trouvé
    if (!$user) {
        return $this->json([
            'success' => false,
            'message' => 'QR code invalide ou client introuvable',
            'data' => null
        ]);
    }

    // 5. Retourner les infos fidélité
    return $this->json([
        'success' => true,
        'message' => 'Client trouvé',
        'data' => [
            'userId' => $user->getId(),
            'nom' => $user->getNom(),
            'prenom' => $user->getPrenom(),
            'email' => $user->getEmail(),
            'couronnes' => $user->getFidelite() ?? 0  // fidelite -> couronnes
        ]
    ]);
}
```

---

## 3. POST /api/mobile/applyLoyaltyReduction

### Description
Applique une réduction fidélité sur une commande en utilisant les points fidélité du client.

### Authentification
? **Requise** (Bearer Token)

### Requête
```http
POST /api/mobile/applyLoyaltyReduction
Authorization: Bearer <token>
Content-Type: application/json
```

### Corps de la requête
```json
{
    "commandeId": 456,
    "userId": 123,
    "couronnesUtilisees": 100,
    "montantReduction": 1.00
}
```

| Paramètre | Type | Obligatoire | Description |
|-----------|------|-------------|-------------|
| `commandeId` | int | ? | ID de la commande/réservation |
| `userId` | int | ? | ID du client |
| `couronnesUtilisees` | int | ? | Nombre de points à déduire |
| `montantReduction` | double | ? | Montant de la réduction (points × 0.01) |

### Réponse - Succès (200)
```json
{
    "success": true,
    "message": "Réduction appliquée avec succès",
    "nouveauSoldeCouronnes": 50,
    "nouveauMontantCommande": 24.50,
    "reductionAppliquee": 1.00
}
```

### Réponse - Échec (200)
```json
{
    "success": false,
    "message": "Solde de points fidélité insuffisant",
    "nouveauSoldeCouronnes": 0,
    "nouveauMontantCommande": 0,
    "reductionAppliquee": 0
}
```

### Messages d'erreur possibles

| Situation | Message |
|-----------|---------|
| Commande non trouvée | "Commande introuvable" |
| Client non trouvé | "Client introuvable" |
| Commande déjà livrée | "Impossible d'appliquer une réduction sur une commande livrée" |
| Réduction déjà appliquée | "Une réduction fidélité a déjà été appliquée sur cette commande" |
| Solde insuffisant | "Solde de points fidélité insuffisant" |
| Montant invalide | "Le montant de réduction dépasse le montant de la commande" |
| Paramètres invalides | "Paramètres invalides" |

### Schéma de flux

```
???????????????????????????????????????????????????????????????????
?                         DÉBUT API                                ?
???????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????
?  1. Récupérer la commande par commandeId                        ?
?     ? Si non trouvée ? Erreur "Commande introuvable"            ?
???????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????
?  2. Récupérer l'utilisateur par userId                          ?
?     ? Si non trouvé ? Erreur "Client introuvable"               ?
???????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????
?  3. Vérifier que commande.etat ? "Livrée"                       ?
?     ? Si livrée ? Erreur "Commande déjà livrée"                 ?
???????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????
?  4. Vérifier que commande.reductionFidelite == null ou 0        ?
?     ? Si déjà appliquée ? Erreur "Réduction déjà appliquée"     ?
???????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????
?  5. Vérifier user.fidelite >= couronnesUtilisees                ?
?     ? Si insuffisant ? Erreur "Solde insuffisant"               ?
???????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????
?  6. Vérifier montantReduction <= commande.montantTotal          ?
?     ? Si trop élevé ? Ajuster automatiquement                   ?
???????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????
?  7. TRANSACTION :                                                ?
?     - user.fidelite -= pointsUtilises                           ?
?     - commande.reductionFidelite = montantReduction             ?
?     - commande.userIdFidelite = userId                          ?
?     - commande.pointsFideliteUtilises = pointsUtilises          ?
?     - Sauvegarder user + commande                               ?
???????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????
?  8. Retourner :                                                  ?
?     - success: true                                              ?
?     - nouveauSoldeCouronnes: user.fidelite                      ?
?     - nouveauMontantCommande: montantOriginal - réduction       ?
?     - reductionAppliquee: montantReduction                      ?
???????????????????????????????????????????????????????????????????
```

### Implémentation Symfony
```php
#[Route('/api/mobile/applyLoyaltyReduction', name: 'apply_loyalty_reduction', methods: ['POST'])]
public function applyLoyaltyReduction(
    Request $request,
    EntityManagerInterface $em
): JsonResponse
{
    // 1. Décoder le JSON
    $data = json_decode($request->getContent(), true);
    
    $commandeId = $data['commandeId'] ?? null;
    $userId = $data['userId'] ?? null;
    $pointsUtilises = $data['couronnesUtilisees'] ?? 0;
    $montantReduction = $data['montantReduction'] ?? 0;

    // 2. Valider les paramètres
    if (!$commandeId || !$userId || $pointsUtilises <= 0) {
        return $this->json([
            'success' => false,
            'message' => 'Paramètres invalides',
            'nouveauSoldeCouronnes' => 0,
            'nouveauMontantCommande' => 0,
            'reductionAppliquee' => 0
        ]);
    }

    // 3. Récupérer la commande
    $commande = $this->commandeRepository->find($commandeId);
    if (!$commande) {
        return $this->json([
            'success' => false,
            'message' => 'Commande introuvable',
            'nouveauSoldeCouronnes' => 0,
            'nouveauMontantCommande' => 0,
            'reductionAppliquee' => 0
        ]);
    }

    // 4. Récupérer l'utilisateur
    $user = $this->userRepository->find($userId);
    if (!$user) {
        return $this->json([
            'success' => false,
            'message' => 'Client introuvable',
            'nouveauSoldeCouronnes' => 0,
            'nouveauMontantCommande' => 0,
            'reductionAppliquee' => 0
        ]);
    }

    // 5. Vérifier que la commande n'est pas déjà livrée
    if ($commande->getEtat() === 'Livrée') {
        return $this->json([
            'success' => false,
            'message' => 'Impossible d\'appliquer une réduction sur une commande livrée',
            'nouveauSoldeCouronnes' => $user->getFidelite(),
            'nouveauMontantCommande' => $commande->getMontantTotal(),
            'reductionAppliquee' => 0
        ]);
    }

    // 6. Vérifier qu'aucune réduction n'a déjà été appliquée
    if ($commande->getReductionFidelite() > 0) {
        return $this->json([
            'success' => false,
            'message' => 'Une réduction fidélité a déjà été appliquée sur cette commande',
            'nouveauSoldeCouronnes' => $user->getFidelite(),
            'nouveauMontantCommande' => $commande->getMontantTotal(),
            'reductionAppliquee' => 0
        ]);
    }

    // 7. Vérifier le solde fidélité
    $soldeFidelite = $user->getFidelite() ?? 0;
    if ($soldeFidelite < $pointsUtilises) {
        return $this->json([
            'success' => false,
            'message' => 'Solde de points fidélité insuffisant',
            'nouveauSoldeCouronnes' => $soldeFidelite,
            'nouveauMontantCommande' => $commande->getMontantTotal(),
            'reductionAppliquee' => 0
        ]);
    }

    // 8. Vérifier que la réduction ne dépasse pas le montant
    $montantOriginal = $commande->getMontantTotal();
    if ($montantReduction > $montantOriginal) {
        $montantReduction = $montantOriginal;
        $pointsUtilises = (int) ($montantReduction / 0.01);
    }

    // 9. Appliquer la réduction (TRANSACTION)
    try {
        $em->beginTransaction();

        // Déduire les points fidélité du client
        $nouveauSolde = $soldeFidelite - $pointsUtilises;
        $user->setFidelite($nouveauSolde);

        // Enregistrer la réduction sur la commande
        $commande->setReductionFidelite($montantReduction);
        $commande->setUserIdFidelite($userId);
        $commande->setPointsFideliteUtilises($pointsUtilises);

        // Calculer le nouveau montant
        $nouveauMontant = $montantOriginal - $montantReduction;

        // Sauvegarder
        $em->persist($user);
        $em->persist($commande);
        $em->flush();
        $em->commit();

        return $this->json([
            'success' => true,
            'message' => 'Réduction appliquée avec succès',
            'nouveauSoldeCouronnes' => $nouveauSolde,
            'nouveauMontantCommande' => $nouveauMontant,
            'reductionAppliquee' => $montantReduction
        ]);

    } catch (\Exception $e) {
        $em->rollback();
        
        return $this->json([
            'success' => false,
            'message' => 'Erreur lors de l\'application de la réduction',
            'nouveauSoldeCouronnes' => $user->getFidelite(),
            'nouveauMontantCommande' => $commande->getMontantTotal(),
            'reductionAppliquee' => 0
        ]);
    }
}
```

---

## Modifications base de données

### Table `user`
```sql
-- Si le champ fidelite n'existe pas déjà
ALTER TABLE user ADD COLUMN fidelite INT DEFAULT 0;
```

### Table `commande`
```sql
ALTER TABLE commande ADD COLUMN reduction_fidelite DECIMAL(10,2) DEFAULT 0;
ALTER TABLE commande ADD COLUMN user_id_fidelite INT DEFAULT NULL;
ALTER TABLE commande ADD COLUMN points_fidelite_utilises INT DEFAULT 0;
```

---

## Entités Doctrine

### User.php
```php
#[ORM\Column(type: 'integer', options: ['default' => 0])]
private int $fidelite = 0;

public function getFidelite(): int
{
    return $this->fidelite;
}

public function setFidelite(int $fidelite): self
{
    $this->fidelite = $fidelite;
    return $this;
}
```

### Commande.php
```php
#[ORM\Column(type: 'decimal', precision: 10, scale: 2, options: ['default' => 0])]
private float $reductionFidelite = 0;

#[ORM\Column(type: 'integer', nullable: true)]
private ?int $userIdFidelite = null;

#[ORM\Column(type: 'integer', options: ['default' => 0])]
private int $pointsFideliteUtilises = 0;

public function getReductionFidelite(): float
{
    return $this->reductionFidelite;
}

public function setReductionFidelite(float $reductionFidelite): self
{
    $this->reductionFidelite = $reductionFidelite;
    return $this;
}

public function getUserIdFidelite(): ?int
{
    return $this->userIdFidelite;
}

public function setUserIdFidelite(?int $userIdFidelite): self
{
    $this->userIdFidelite = $userIdFidelite;
    return $this;
}

public function getPointsFideliteUtilises(): int
{
    return $this->pointsFideliteUtilises;
}

public function setPointsFideliteUtilises(int $points): self
{
    $this->pointsFideliteUtilises = $points;
    return $this;
}
```

---

## Correspondance des champs

| App Mobile (JSON) | Symfony (Entity) | Description |
|-------------------|------------------|-------------|
| `couronnes` | `fidelite` | Points fidélité du client |
| `couronnesUtilisees` | `pointsFideliteUtilises` | Points utilisés pour la réduction |
| `nouveauSoldeCouronnes` | `fidelite` (après déduction) | Nouveau solde après réduction |

---

## Tests recommandés

### Tests fonctionnels

| # | Scénario | Résultat attendu |
|---|----------|------------------|
| 1 | Scanner un QR code valide | Client trouvé avec ses points fidélité |
| 2 | Scanner un QR code invalide | Erreur "Client introuvable" |
| 3 | Appliquer une réduction avec solde suffisant | Succès, points déduits |
| 4 | Appliquer une réduction avec solde insuffisant | Erreur "Solde insuffisant" |
| 5 | Appliquer une réduction sur commande livrée | Erreur "Commande livrée" |
| 6 | Appliquer une seconde réduction sur même commande | Erreur "Déjà appliquée" |
| 7 | Réduction supérieure au montant commande | Ajustement automatique |

### Tests de sécurité

| # | Test | Vérification |
|---|------|--------------|
| 1 | Appel sans token | Erreur 401 Unauthorized |
| 2 | Appel avec token expiré | Erreur 401 Unauthorized |
| 3 | commandeId inexistant | Erreur "Commande introuvable" |
| 4 | userId inexistant | Erreur "Client introuvable" |

---

## Résumé des règles métier

| Règle | Valeur |
|-------|--------|
| Champ Symfony pour les points | `fidelite` |
| Champ JSON pour l'app | `couronnes` |
| Conversion points ? euros | **10 points = 0.10 €** |
| 1 point = | **0.01 €** |
| Réduction max par commande | Montant total de la commande |
| États autorisant la réduction | Confirmée, En cours, Traitée |
| État bloquant la réduction | Livrée |
| Une seule réduction fidélité par commande | ? Oui |

---

*Document généré pour le projet GDM2026 - Dantec Market*
