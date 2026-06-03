/**
 * Sync SagittalPlayer keyframes to HTMLAudioElement playback.
 */
export class KeyframeSync {
  /**
   * @param {{ playKeyframes: (kf: object[], t: number) => void }} player
   * @param {HTMLAudioElement} audio
   * @param {Array<{ startMs?: number; endMs?: number; layers?: object }>} keyframes
   */
  constructor(player, audio, keyframes) {
    this.player = player
    this.audio = audio
    this.keyframes = keyframes ?? []
    this._raf = 0
    this._onEnded = () => this.stop()
    audio.addEventListener('ended', this._onEnded)
  }

  start() {
    this.stop()
    const tick = () => {
      const ms = this.audio.currentTime * 1000
      this.player.playKeyframes(this.keyframes, ms)
      this._raf = requestAnimationFrame(tick)
    }
    this._raf = requestAnimationFrame(tick)
  }

  stop() {
    if (this._raf) {
      cancelAnimationFrame(this._raf)
      this._raf = 0
    }
  }

  dispose() {
    this.stop()
    this.audio.removeEventListener('ended', this._onEnded)
  }
}
