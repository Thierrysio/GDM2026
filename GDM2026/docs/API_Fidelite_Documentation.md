# Documentation API - Fonctionnalit� Fid�lit�

**Application** : GDM2026 - Dantec Market  
**Version** : 1.0  
**Date** : Janvier 2025

---

## Table des mati�res

1. [Vue d'ensemble](#vue-densemble)
2. [GET /api/mobile/getNombreCommandes](#1-get-apimobilegetnombrecommandes)
3. [POST /api/mobile/getLoyaltyByQrCode](#2-post-apimobilegetloyaltybyqrcode)
4. [POST /api/mobile/applyLoyaltyReduction](#3-post-apimobileapplyloyaltyreduction)
5. [Modifications base de donn�es](#modifications-base-de-donn�es)
6. [Entit�s Doctrine](#entit�s-doctrine)
7. [Correspondance des champs](#correspondance-des-champs)
8. [Tests recommand�s](#tests-recommand�s)

---

## Vue d'ensemble

### Objectif
Permettre aux administrateurs d'appliquer des r�ductions fid�lit� sur les commandes des clients en scannant leur QR code.

### R�gle de conversion
| Points fid�lit� | Valeur en euros |
|-----------------|-----------------|
| 1 point | 0.0667 � |
| 15 points | 1.00 � |
| 150 points | 10.00 � |

### Flux utilisateur
1. L'admin ouvre une commande/r�servation
2. L'admin clique sur le bouton "Fid�lit�"
3. L'admin scanne le QR code du client
4. L'app affiche les points disponibles du client
5. L'admin choisit combien de points utiliser
6. La r�duction est appliqu�e et les points sont d�duits

---

## 1. GET /api/mobile/getNombreCommandes

### Description
Retourne le nombre de commandes/r�servations group�es par �tat.

### Authentification
? **Requise** (Bearer Token)

### Requ�te
```http
GET /api/mobile/getNombreCommandes
Authorization: Bearer <token>
```

### R�ponse - Succ�s (200)
```json
{
    "Confirm�e": 12,
    "En cours de traitement": 5,
    "Trait�e": 8,
    "Livr�e": 45,
    "A confirmer": 3
}
```

### Impl�mentation Symfony
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
R�cup�re les informations de fid�lit� d'un client � partir du contenu de son QR code.

### Authentification
? **Requise** (Bearer Token)

### Requ�te
```http
POST /api/mobile/getLoyaltyByQrCode
Authorization: Bearer <token>
Content-Type: application/json
```

### Corps de la requ�te
```json
{
    "qrCode": "string"
}
```

| Param�tre | Type | Obligatoire | Description |
|-----------|------|-------------|-------------|
| `qrCode` | string | ? | Contenu du QR code scann� |

### R�ponse - Succ�s (200) - Client trouv�
```json
{
    "success": true,
    "message": "Client trouv�",
    "data": {
        "userId": 123,
        "nom": "Dupont",
        "prenom": "Jean",
        "email": "jean.dupont@email.com",
        "couronnes": 150
    }
}
```

> ?? **Note** : Le champ JSON s'appelle `couronnes` pour l'app mobile, mais c�t� Symfony vous utilisez `fidelite`.

### R�ponse - �chec (200) - Client non trouv�
```json
{
    "success": false,
    "message": "QR code invalide ou client introuvable",
    "data": null
}
```

### R�ponse - �chec (200) - QR code manquant
```json
{
    "success": false,
    "message": "QR code manquant",
    "data": null
}
```

### Impl�mentation Symfony
```php
#[Route('/api/mobile/getLoyaltyByQrCode', name: 'get_loyalty_by_qrcode', methods: ['POST'])]
public function getLoyaltyByQrCode(Request $request): JsonResponse
{
    // 1. D�coder le JSON
    $data = json_decode($request->getContent(), true);
    $qrCode = $data['qrCode'] ?? null;

    // 2. Valider le param�tre
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

    // 4. V�rifier si trouv�
    if (!$user) {
        return $this->json([
            'success' => false,
            'message' => 'QR code invalide ou client introuvable',
            'data' => null
        ]);
    }

    // 5. Retourner les infos fid�lit�
    return $this->json([
        'success' => true,
        'message' => 'Client trouv�',
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
Applique une r�duction fid�lit� sur une commande en utilisant les points fid�lit� du client.

### Authentification
? **Requise** (Bearer Token)

### Requ�te
```http
POST /api/mobile/applyLoyaltyReduction
Authorization: Bearer <token>
Content-Type: application/json
```

### Corps de la requ�te
```json
{
    "commandeId": 456,
    "userId": 123,
    "couronnesUtilisees": 100,
    "montantReduction": 1.00
}
```

| Param�tre | Type | Obligatoire | Description |
|-----------|------|-------------|-------------|
| `commandeId` | int | ? | ID de la commande/r�servation |
| `userId` | int | ? | ID du client |
| `couronnesUtilisees` | int | ? | Nombre de points � d�duire |
| `montantReduction` | double | ? | Montant de la r�duction (points / 15) |

### R�ponse - Succ�s (200)
```json
{
    "success": true,
    "message": "R�duction appliqu�e avec succ�s",
    "nouveauSoldeCouronnes": 50,
    "nouveauMontantCommande": 24.50,
    "reductionAppliquee": 1.00
}
```

### R�ponse - �chec (200)
```json
{
    "success": false,
    "message": "Solde de points fid�lit� insuffisant",
    "nouveauSoldeCouronnes": 0,
    "nouveauMontantCommande": 0,
    "reductionAppliquee": 0
}
```

### Messages d'erreur possibles

| Situation | Message |
|-----------|---------|
| Commande non trouv�e | "Commande introuvable" |
| Client non trouv� | "Client introuvable" |
| Commande d�j� livr�e | "Impossible d'appliquer une r�duction sur une commande livr�e" |
| R�duction d�j� appliqu�e | "Une r�duction fid�lit� a d�j� �t� appliqu�e sur cette commande" |
| Solde insuffisant | "Solde de points fid�lit� insuffisant" |
| Montant invalide | "Le montant de r�duction d�passe le montant de la commande" |
| Param�tres invalides | "Param�tres invalides" |

### Sch�ma de flux

```
???????????????????????????????????????????????????????????????????
?                         D�BUT API                                ?
???????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????
?  1. R�cup�rer la commande par commandeId                        ?
?     ? Si non trouv�e ? Erreur "Commande introuvable"            ?
???????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????
?  2. R�cup�rer l'utilisateur par userId                          ?
?     ? Si non trouv� ? Erreur "Client introuvable"               ?
???????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????
?  3. V�rifier que commande.etat ? "Livr�e"                       ?
?     ? Si livr�e ? Erreur "Commande d�j� livr�e"                 ?
???????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????
?  4. V�rifier que commande.reductionFidelite == null ou 0        ?
?     ? Si d�j� appliqu�e ? Erreur "R�duction d�j� appliqu�e"     ?
???????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????
?  5. V�rifier user.fidelite >= couronnesUtilisees                ?
?     ? Si insuffisant ? Erreur "Solde insuffisant"               ?
???????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????
?  6. V�rifier montantReduction <= commande.montantTotal          ?
?     ? Si trop �lev� ? Ajuster automatiquement                   ?
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
?     - nouveauMontantCommande: montantOriginal - r�duction       ?
?     - reductionAppliquee: montantReduction                      ?
???????????????????????????????????????????????????????????????????
```

### Impl�mentation Symfony
```php
#[Route('/api/mobile/applyLoyaltyReduction', name: 'apply_loyalty_reduction', methods: ['POST'])]
public function applyLoyaltyReduction(
    Request $request,
    EntityManagerInterface $em
): JsonResponse
{
    // 1. D�coder le JSON
    $data = json_decode($request->getContent(), true);
    
    $commandeId = $data['commandeId'] ?? null;
    $userId = $data['userId'] ?? null;
    $pointsUtilises = $data['couronnesUtilisees'] ?? 0;
    $montantReduction = $data['montantReduction'] ?? 0;

    // 2. Valider les param�tres
    if (!$commandeId || !$userId || $pointsUtilises <= 0) {
        return $this->json([
            'success' => false,
            'message' => 'Param�tres invalides',
            'nouveauSoldeCouronnes' => 0,
            'nouveauMontantCommande' => 0,
            'reductionAppliquee' => 0
        ]);
    }

    // 3. R�cup�rer la commande
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

    // 4. R�cup�rer l'utilisateur
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

    // 5. V�rifier que la commande n'est pas d�j� livr�e
    if ($commande->getEtat() === 'Livr�e') {
        return $this->json([
            'success' => false,
            'message' => 'Impossible d\'appliquer une r�duction sur une commande livr�e',
            'nouveauSoldeCouronnes' => $user->getFidelite(),
            'nouveauMontantCommande' => $commande->getMontantTotal(),
            'reductionAppliquee' => 0
        ]);
    }

    // 6. V�rifier qu'aucune r�duction n'a d�j� �t� appliqu�e
    if ($commande->getReductionFidelite() > 0) {
        return $this->json([
            'success' => false,
            'message' => 'Une r�duction fid�lit� a d�j� �t� appliqu�e sur cette commande',
            'nouveauSoldeCouronnes' => $user->getFidelite(),
            'nouveauMontantCommande' => $commande->getMontantTotal(),
            'reductionAppliquee' => 0
        ]);
    }

    // 7. V�rifier le solde fid�lit�
    $soldeFidelite = $user->getFidelite() ?? 0;
    if ($soldeFidelite < $pointsUtilises) {
        return $this->json([
            'success' => false,
            'message' => 'Solde de points fid�lit� insuffisant',
            'nouveauSoldeCouronnes' => $soldeFidelite,
            'nouveauMontantCommande' => $commande->getMontantTotal(),
            'reductionAppliquee' => 0
        ]);
    }

    // 8. V�rifier que la r�duction ne d�passe pas le montant
    $montantOriginal = $commande->getMontantTotal();
    if ($montantReduction > $montantOriginal) {
        $montantReduction = $montantOriginal;
        $pointsUtilises = (int) ($montantReduction * 15);
    }

    // 9. Appliquer la r�duction (TRANSACTION)
    try {
        $em->beginTransaction();

        // D�duire les points fid�lit� du client
        $nouveauSolde = $soldeFidelite - $pointsUtilises;
        $user->setFidelite($nouveauSolde);

        // Enregistrer la r�duction sur la commande
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
            'message' => 'R�duction appliqu�e avec succ�s',
            'nouveauSoldeCouronnes' => $nouveauSolde,
            'nouveauMontantCommande' => $nouveauMontant,
            'reductionAppliquee' => $montantReduction
        ]);

    } catch (\Exception $e) {
        $em->rollback();
        
        return $this->json([
            'success' => false,
            'message' => 'Erreur lors de l\'application de la r�duction',
            'nouveauSoldeCouronnes' => $user->getFidelite(),
            'nouveauMontantCommande' => $commande->getMontantTotal(),
            'reductionAppliquee' => 0
        ]);
    }
}
```

---

## Modifications base de donn�es

### Table `user`
```sql
-- Si le champ fidelite n'existe pas d�j�
ALTER TABLE user ADD COLUMN fidelite INT DEFAULT 0;
```

### Table `commande`
```sql
ALTER TABLE commande ADD COLUMN reduction_fidelite DECIMAL(10,2) DEFAULT 0;
ALTER TABLE commande ADD COLUMN user_id_fidelite INT DEFAULT NULL;
ALTER TABLE commande ADD COLUMN points_fidelite_utilises INT DEFAULT 0;
```

---

## Entit�s Doctrine

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
| `couronnes` | `fidelite` | Points fid�lit� du client |
| `couronnesUtilisees` | `pointsFideliteUtilises` | Points utilis�s pour la r�duction |
| `nouveauSoldeCouronnes` | `fidelite` (apr�s d�duction) | Nouveau solde apr�s r�duction |

---

## Tests recommand�s

### Tests fonctionnels

| # | Sc�nario | R�sultat attendu |
|---|----------|------------------|
| 1 | Scanner un QR code valide | Client trouv� avec ses points fid�lit� |
| 2 | Scanner un QR code invalide | Erreur "Client introuvable" |
| 3 | Appliquer une r�duction avec solde suffisant | Succ�s, points d�duits |
| 4 | Appliquer une r�duction avec solde insuffisant | Erreur "Solde insuffisant" |
| 5 | Appliquer une r�duction sur commande livr�e | Erreur "Commande livr�e" |
| 6 | Appliquer une seconde r�duction sur m�me commande | Erreur "D�j� appliqu�e" |
| 7 | R�duction sup�rieure au montant commande | Ajustement automatique |

### Tests de s�curit�

| # | Test | V�rification |
|---|------|--------------|
| 1 | Appel sans token | Erreur 401 Unauthorized |
| 2 | Appel avec token expir� | Erreur 401 Unauthorized |
| 3 | commandeId inexistant | Erreur "Commande introuvable" |
| 4 | userId inexistant | Erreur "Client introuvable" |

---

## R�sum� des r�gles m�tier

| R�gle | Valeur |
|-------|--------|
| Champ Symfony pour les points | `fidelite` |
| Champ JSON pour l'app | `couronnes` |
| Conversion points ? euros | **15 points = 1.00 �** |
| 1 point = | **0.0667 �** |
| R�duction max par commande | Montant total de la commande |
| �tats autorisant la r�duction | Confirm�e, En cours, Trait�e |
| �tat bloquant la r�duction | Livr�e |
| Une seule r�duction fid�lit� par commande | ? Oui |

---

*Document g�n�r� pour le projet GDM2026 - Dantec Market*
