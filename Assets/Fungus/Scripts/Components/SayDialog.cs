// This code is part of the Fungus library (http://fungusgames.com) maintained by Chris Gregan (http://twitter.com/gofungus).
// It is released for free under the MIT open source license (https://github.com/snozbot/fungus/blob/master/LICENSE)

using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

namespace Fungus
{
    /// <summary>
    /// Display story text in a visual novel style dialog box.
    /// </summary>
    public class SayDialog : MonoBehaviour
    {
        [Tooltip("Duration to fade dialogue in/out")]
        [SerializeField] protected float fadeDuration = 0.25f;

        [Tooltip("The continue button UI object")]
        [SerializeField] protected Button continueButton;

        [Tooltip("The canvas UI object")]
        [SerializeField] protected Canvas dialogCanvas;

        [Tooltip("The name text UI object")]
        [SerializeField] protected Text nameText;

        [Tooltip("The story text UI object")]
        [SerializeField] protected Text storyText;
        public virtual Text StoryText { get { return storyText; } }

        [Tooltip("The character UI object")]
        [SerializeField] protected Image characterImage;
        public virtual Image CharacterImage { get { return characterImage; } }
    
        [Tooltip("Adjust width of story text when Character Image is displayed (to avoid overlapping)")]
        [SerializeField] protected bool fitTextWithImage = true;

        protected float startStoryTextWidth; 
        protected float startStoryTextInset;

        protected WriterAudio writerAudio;
        protected IWriter writer;
        protected CanvasGroup canvasGroup;

        protected bool fadeWhenDone = true;
        protected float targetAlpha = 0f;
        protected float fadeCoolDownTimer = 0f;

        protected Sprite currentCharacterImage;

        // Currently active Say Dialog used to display Say text
        public static SayDialog activeSayDialog;

        // Most recent speaking character
        public static Character speakingCharacter;

        public static SayDialog GetSayDialog()
        {
            if (activeSayDialog == null)
            {
                // Use first Say Dialog found in the scene (if any)
                SayDialog sd = GameObject.FindObjectOfType<SayDialog>();
                if (sd != null)
                {
                    activeSayDialog = sd;
                }
                
                if (activeSayDialog == null)
                {
                    // Auto spawn a say dialog object from the prefab
                    GameObject prefab = Resources.Load<GameObject>("Prefabs/SayDialog");
                    if (prefab != null)
                    {
                        GameObject go = Instantiate(prefab) as GameObject;
                        go.SetActive(false);
                        go.name = "SayDialog";
                        activeSayDialog = go.GetComponent<SayDialog>();
                    }
                }
            }
            
            return activeSayDialog;
        }
            
        protected IWriter GetWriter()
        {
            if (writer != null)
            {
                return writer;
            }

            writer = GetComponent<IWriter>();
            if (writer == null)
            {
                writer = gameObject.AddComponent<Writer>();
            }

            return writer;
        }

        protected CanvasGroup GetCanvasGroup()
        {
            if (canvasGroup != null)
            {
                return canvasGroup;
            }
            
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            
            return canvasGroup;
        }

        protected WriterAudio GetWriterAudio()
        {
            if (writerAudio != null)
            {
                return writerAudio;
            }
            
            writerAudio = GetComponent<WriterAudio>();
            if (writerAudio == null)
            {
                writerAudio = gameObject.AddComponent<WriterAudio>();
            }
            
            return writerAudio;
        }

        protected void Start()
        {
            // Dialog always starts invisible, will be faded in when writing starts
            GetCanvasGroup().alpha = 0f;

            // Add a raycaster if none already exists so we can handle dialog input
            GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();    
            }

            // It's possible that SetCharacterImage() has already been called from the
            // Start method of another component, so check that no image has been set yet.
            // Same for nameText.

            if (nameText != null && nameText.text == "")
            {
                SetCharacterName("", Color.white);
            }
            if (currentCharacterImage == null)
            {                
                // Character image is hidden by default.
                SetCharacterImage(null);
            }
        }

        protected virtual void LateUpdate()
        {
            UpdateAlpha();

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive( GetWriter().IsWaitingForInput );
            }
        }

        protected virtual void UpdateAlpha()
        {
            if (GetWriter().IsWriting)
            {
                targetAlpha = 1f;
                fadeCoolDownTimer = 0.1f;
            }
            else if (fadeWhenDone && fadeCoolDownTimer == 0f)
            {
                targetAlpha = 0f;
            }
            else
            {
                // Add a short delay before we start fading in case there's another Say command in the next frame or two.
                // This avoids a noticeable flicker between consecutive Say commands.
                fadeCoolDownTimer = Mathf.Max(0f, fadeCoolDownTimer - Time.deltaTime);
            }

            CanvasGroup canvasGroup = GetCanvasGroup();
            if (fadeDuration <= 0f)
            {
                canvasGroup.alpha = targetAlpha;
            }
            else
            {
                float delta = (1f / fadeDuration) * Time.deltaTime;
                float alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, delta);
                canvasGroup.alpha = alpha;

                if (alpha <= 0f)
                {                   
                    // Deactivate dialog object once invisible
                    gameObject.SetActive(false);
                }
            }
        }

        protected virtual void ClearStoryText()
        {
            if (storyText != null)
            {
                storyText.text = "";
            }
        }

        public static void StopPortraitTweens()
        {
            // Stop all tweening portraits
            foreach( Character c in Character.activeCharacters )
            {
                if (c.State.portraitImage != null)
                {
                    if (LeanTween.isTweening(c.State.portraitImage.gameObject))
                    {
                        LeanTween.cancel(c.State.portraitImage.gameObject, true);
                        
                        PortraitController.SetRectTransform(c.State.portraitImage.rectTransform, c.State.position);
                        if (c.State.dimmed == true)
                        {
                            c.State.portraitImage.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                        }
                        else
                        {
                            c.State.portraitImage.color = Color.white;
                        }
                    }
                }
            }
        }

        #region Public methods

        /// <summary>
        /// Sets the active state of the Say Dialog gameobject.
        /// </summary>
        public virtual void SetActive(bool state)
        {
            gameObject.SetActive(state);
        }

        /// <summary>
        /// Sets the active speaking character.
        /// </summary>
        /// <param name="character">The active speaking character.</param>
        /// <param name="flowchart">An optional Flowchart to use for variable substitution in the character name string.</param>
        public virtual void SetCharacter(Character character, IFlowchart flowchart = null)
        {
            if (character == null)
            {
                if (characterImage != null)
                {
                    characterImage.gameObject.SetActive(false);
                }
                if (nameText != null)
                {
                    nameText.text = "";
                }
                speakingCharacter = null;
            }
            else
            {
                var prevSpeakingCharacter = speakingCharacter;
                speakingCharacter = character;

                // Dim portraits of non-speaking characters
                foreach (Stage stage in Stage.activeStages)
                {

                    if (stage.DimPortraits)
                    {
                        foreach (var c in stage.CharactersOnStage)
                        {
                            if (prevSpeakingCharacter != speakingCharacter)
                            {
                                if (c != null && !c.Equals(speakingCharacter))
                                {
                                    stage.SetDimmed(c, true);
                                }
                                else
                                {
                                    stage.SetDimmed(c, false);
                                }
                            }
                        }
                    }
                }

                string characterName = character.NameText;

                if (characterName == "")
                {
                    // Use game object name as default
                    characterName = character.GetObjectName();
                }

                if (flowchart != null)
                {
                    characterName = flowchart.SubstituteVariables(characterName);
                }

                SetCharacterName(characterName, character.NameColor);
            }
        }

        /// <summary>
        /// Sets the character image to display on the Say Dialog.
        /// </summary>
        public virtual void SetCharacterImage(Sprite image)
        {
            if (characterImage == null)
            {
                return;
            }

            if (image != null)
            {
                characterImage.sprite = image;
                characterImage.gameObject.SetActive(true);
                currentCharacterImage = image;
            }
            else
            {
                characterImage.gameObject.SetActive(false);

                if (startStoryTextWidth != 0)
                {
                    storyText.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 
                        startStoryTextInset, 
                        startStoryTextWidth);
                }
            }

            // Adjust story text box to not overlap image rect
            if (fitTextWithImage && 
                storyText != null &&
                characterImage.gameObject.activeSelf)
            {
                if (startStoryTextWidth == 0)
                {
                    startStoryTextWidth = storyText.rectTransform.rect.width;
                    startStoryTextInset = storyText.rectTransform.offsetMin.x; 
                }

                // Clamp story text to left or right depending on relative position of the character image
                if (storyText.rectTransform.position.x < characterImage.rectTransform.position.x)
                {
                    storyText.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 
                        startStoryTextInset, 
                        startStoryTextWidth - characterImage.rectTransform.rect.width);
                }
                else
                {
                    storyText.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Right, 
                        startStoryTextInset, 
                        startStoryTextWidth - characterImage.rectTransform.rect.width);
                }
            }
        }

        /// <summary>
        /// Sets the character name to display on the Say Dialog.
        /// </summary>
        public virtual void SetCharacterName(string name, Color color)
        {
            if (nameText != null)
            {
                nameText.text = name;
                nameText.color = color;
            }
        }

        /// <summary>
        /// Write a line of story text to the Say Dialog. Starts coroutine automatically.
        /// </summary>
        /// <param name="text">The text to display.</param>
        /// <param name="clearPrevious">Clear any previous text in the Say Dialog.</param>
        /// <param name="waitForInput">Wait for player input before continuing once text is written.</param>
        /// <param name="fadeWhenDone">Fade out the Say Dialog when writing and player input has finished.</param>
        /// <param name="stopVoiceover">Stop any existing voiceover audio before writing starts.</param>
        /// <param name="voiceOverClip">Voice over audio clip to play.</param>
        /// <param name="onComplete">Callback to execute when writing and player input have finished.</param>
        public virtual void Say(string text, bool clearPrevious, bool waitForInput, bool fadeWhenDone, bool stopVoiceover, AudioClip voiceOverClip, Action onComplete)
        {
            StartCoroutine(DoSay(text, clearPrevious, waitForInput, fadeWhenDone, stopVoiceover, voiceOverClip, onComplete));
        }

        /// <summary>
        /// Write a line of story text to the Say Dialog. Must be started as a coroutine.
        /// </summary>
        /// <param name="text">The text to display.</param>
        /// <param name="clearPrevious">Clear any previous text in the Say Dialog.</param>
        /// <param name="waitForInput">Wait for player input before continuing once text is written.</param>
        /// <param name="fadeWhenDone">Fade out the Say Dialog when writing and player input has finished.</param>
        /// <param name="stopVoiceover">Stop any existing voiceover audio before writing starts.</param>
        /// <param name="voiceOverClip">Voice over audio clip to play.</param>
        /// <param name="onComplete">Callback to execute when writing and player input have finished.</param>
        public virtual IEnumerator DoSay(string text, bool clearPrevious, bool waitForInput, bool fadeWhenDone, bool stopVoiceover, AudioClip voiceOverClip, Action onComplete)
        {
            IWriter writer = GetWriter();

            if (writer.IsWriting || writer.IsWaitingForInput)
            {
                writer.Stop();
                while (writer.IsWriting || writer.IsWaitingForInput)
                {
                    yield return null;
                }
            }

            gameObject.SetActive(true);

            this.fadeWhenDone = fadeWhenDone;

            // Voice over clip takes precedence over a character sound effect if provided

            AudioClip soundEffectClip = null;
            if (voiceOverClip != null)
            {
                WriterAudio writerAudio = GetWriterAudio();
                writerAudio.OnVoiceover(voiceOverClip);
            }
            else if (speakingCharacter != null)
            {
                soundEffectClip = speakingCharacter.SoundEffect;
            }

            yield return StartCoroutine(writer.Write(text, clearPrevious, waitForInput, stopVoiceover, soundEffectClip, onComplete));
        }

        /// <summary>
        /// Tell the Say Dialog to fade out once writing and player input have finished.
        /// </summary>
        public virtual bool FadeWhenDone { set { fadeWhenDone = value; } }

        /// <summary>
        /// Stop the Say Dialog while its writing text.
        /// </summary>
        public virtual void Stop()
        {
            fadeWhenDone = true;
            GetWriter().Stop();
        }

        /// <summary>
        /// Stops writing text and clears the Say Dialog.
        /// </summary>
        public virtual void Clear()
        {
            ClearStoryText();

            // Kill any active write coroutine
            StopAllCoroutines();
        }

        #endregion
    }
}
