using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class CharacterCarousel : MonoBehaviour
{
    [Header("Cinematic Effects")]
    public AudioSource audioSource;
    public AudioClip tickSound;
    public AudioClip finalHitSound;

    public Image flashImage;
    public Camera mainCamera;

    private bool isSpinning = false;

    public RectTransform cardContainer;
    public float spacing = 300f;

    private int currentIndex = 0;

    void Start()
    {
        UpdateSelection();
    }

    void Update()
    {
        if (isSpinning) return;   // ✅ Prevent input during spin

        if (Input.GetKeyDown(KeyCode.RightArrow))
            Next();

        if (Input.GetKeyDown(KeyCode.LeftArrow))
            Previous();

        if (Input.GetKeyDown(KeyCode.Return))
            Select();
    }

    public void Next()
    {
        if (currentIndex < cardContainer.childCount - 1)
        {
            currentIndex++;
            Slide();
        }
    }

    public void Previous()
    {
        if (currentIndex > 0)
        {
            currentIndex--;
            Slide();
        }
    }

    void Slide()
    {
        float targetX = -currentIndex * spacing;

        cardContainer.DOKill();
        cardContainer.DOAnchorPosX(targetX, 0.4f)
            .SetEase(Ease.OutCubic);

        UpdateSelection();
    }

    public void RandomSelect()
    {
        if (!isSpinning)
            StartCoroutine(RandomSpinCinematic());
    }



    public void Player1Ready()
    {
        string selectedName = cardContainer.GetChild(currentIndex).name;

        GameManager.Instance.player1Character = selectedName;

        SceneTransition.Instance.FadeToScene("CharacterSelection_2");
    }


    System.Collections.IEnumerator RandomSpinCinematic()
    {
        isSpinning = true;

        int totalCards = cardContainer.childCount;

        int spinRounds = Random.Range(20, 30);
        float delay = 0.04f;

        for (int i = 0; i < spinRounds; i++)
        {
            currentIndex = (currentIndex + 1) % totalCards;

            Slide();

            if (tickSound != null && audioSource != null)
                audioSource.PlayOneShot(tickSound);

            yield return new WaitForSeconds(delay);

            delay += 0.008f;
        }

        int finalIndex = Random.Range(0, totalCards);
        currentIndex = finalIndex;

        Slide();

        yield return new WaitForSeconds(0.2f);

        // Flash
        if (flashImage != null)
        {
            flashImage.DOFade(1f, 0.1f).OnComplete(() =>
            {
                flashImage.DOFade(0f, 0.2f);
            });
        }

        // Camera shake
        if (mainCamera != null)
            mainCamera.transform.DOShakePosition(0.3f, 0.3f, 20, 90);

        // Final punch
        Transform selectedCard = cardContainer.GetChild(currentIndex);
        selectedCard.DOPunchScale(Vector3.one * 0.3f, 0.4f, 10, 1);

        if (finalHitSound != null && audioSource != null)
            audioSource.PlayOneShot(finalHitSound);

        isSpinning = false;
    }

    void UpdateSelection()
    {
        for (int i = 0; i < cardContainer.childCount; i++)
        {
            Transform card = cardContainer.GetChild(i);

            card.DOKill();

            if (i == currentIndex)
            {
                card.DOScale(1.2f, 0.2f)
                    .SetEase(Ease.OutBack);
            }
            else
            {
                card.DOScale(0.9f, 0.2f)
                    .SetEase(Ease.OutQuad);
            }
        }
    }

    void Select()
    {
        Transform selectedCard = cardContainer.GetChild(currentIndex);

        selectedCard.DOPunchScale(Vector3.one * 0.2f, 0.3f, 10, 1);

        Debug.Log("Selected: " + selectedCard.name);
    }
}