package musicplayer

import (
	"fmt"
	"math/rand"
	"time"

	"ramiel/pkg/youtube"

	"github.com/bwmarrin/discordgo"
)

type MusicPlayer struct {
	session         *discordgo.Session
	lavalink        *LavalinkManager
	channelID       string
	queue           []*PlayerQueueItem
	activeSong      *PlayerQueueItem
	isPlaying       bool
	voiceConnection *discordgo.VoiceConnection
	loopQueue       bool
	loopSong        bool
	skip            chan bool
	replay          chan bool
}

func New(session *discordgo.Session, voiceState *discordgo.VoiceState) (*MusicPlayer, error) {
	lavalink, err := NewLavalinkManager("lavalink:2333", "youshallnotpass", session)
	if err != nil {
		return nil, err
	}

	voiceConnection, err := session.ChannelVoiceJoin(voiceState.GuildID, voiceState.ChannelID, false, true)
	if err != nil {
		return nil, err
	}

	return &MusicPlayer{
		session:         session,
		lavalink:        lavalink,
		channelID:       voiceState.ChannelID,
		isPlaying:       false,
		queue:           make([]*PlayerQueueItem, 0),
		loopQueue:       false,
		loopSong:        false,
		skip:            make(chan bool),
		replay:          make(chan bool),
		voiceConnection: voiceConnection,
	}, nil
}

func (p *MusicPlayer) GetChannelID() string {
	return p.channelID
}

func (p *MusicPlayer) AddPlaylistToQueue(member *discordgo.Member, url string) (*PlaylistInfo, error) {
	playlist, err := youtube.ResolvePlaylistData(url)
	if err != nil {
		return nil, fmt.Errorf("Failed to get playlist info: %v", err)
	}

	if playlist == nil {
		return nil, nil
	}

	requestedBy := fmt.Sprintf("%s (%s#%s)", member.Nick, member.User.Username, member.User.ID)
	playlistInfo := newPlaylistInfo(requestedBy, playlist)

	p.queue = append(p.queue, playlistInfo.Items...)

	return playlistInfo, nil
}

func (p *MusicPlayer) AddSongToQueue(member *discordgo.Member, url string) (*PlayerQueueItem, error) {
	video, err := youtube.ResolveVideoData(url)
	if err != nil {
		return nil, err
	}

	requestedBy := fmt.Sprintf("%s (%s#%s)", member.Nick, member.User.Username, member.User.ID)
	queueItem := newPlayerQueueItem(requestedBy, video)

	p.queue = append(p.queue, queueItem)

	return queueItem, nil
}

func (p *MusicPlayer) Play() error {
	if p.isPlaying {
		return nil
	}

	p.isPlaying = true

	var err error
	for len(p.queue) > 0 && p.isPlaying {
		err = p.playCurrentSong()
		if err != nil {
			break
		}
	}

	p.isPlaying = false

	return err
}

func (p *MusicPlayer) Stop() {
	p.lavalink.Player.Pause(true)
}

func (p *MusicPlayer) Resume() {
	p.lavalink.Player.Pause(false)
}

func (p *MusicPlayer) Queue() []*PlayerQueueItem {
	return p.queue
}

func (p *MusicPlayer) Shuffle() {
	rand.Seed(time.Now().UnixNano())
	rand.Shuffle(len(p.queue), func(i, j int) { p.queue[i], p.queue[j] = p.queue[j], p.queue[i] })
}

func (p *MusicPlayer) ClearQueue() {
	p.queue = p.queue[:1]
}

func (p *MusicPlayer) RemoveDuplicates() {
	keys := make(map[string]bool)
	list := make([]*PlayerQueueItem, 0)
	for _, entry := range p.queue {
		if _, value := keys[entry.VideoID]; !value {
			keys[entry.VideoID] = true
			list = append(list, entry)
		}
	}
	p.queue = list
}

func (p *MusicPlayer) NowPlaying() *PlayerQueueItem {
	return p.activeSong
}

func (p *MusicPlayer) Skip() {
	p.skip <- true
}

func (p *MusicPlayer) Replay() {
	p.replay <- true
}

func (p *MusicPlayer) RemoveSongFromQueue(item *PlayerQueueItem) {
	if len(p.queue) == 0 {
		return
	}

	sIdx := p.findSongIndex(item.VideoID)
	p.queue = append(p.queue[:sIdx], p.queue[sIdx+1:]...)
}

func (p *MusicPlayer) Exit() {
	p.ClearQueue()
	p.Skip()

	if p.voiceConnection != nil {
		p.voiceConnection.Disconnect()
	}
}

func (p *MusicPlayer) playCurrentSong() error {
	if p.queue[0].Video == nil {
		video, err := youtube.ResolveVideoData(p.queue[0].Url)
		if err != nil {
			return err
		}

		p.queue[0] = newPlayerQueueItem(p.queue[0].RequestedBy, video)
	}

	p.activeSong = p.queue[0]

	err := p.lavalink.Play(p.activeSong.Url)
	if err != nil {
		return err
	}

	for {
		select {
		case <-p.lavalink.isTrackEnded:
			return nil
		case <-p.skip:
			p.loopSong = false
			return nil
		case <-p.replay:
			p.lavalink.Player.Seek(0)
		}
	}
}

func (p *MusicPlayer) postSongHandling(item *PlayerQueueItem) {
	p.activeSong = nil

	if p.loopQueue {
		sIdx := p.findSongIndex(item.VideoID)
		p.queue = append(p.queue[:sIdx], p.queue[sIdx+1:]...)
		p.queue = append(p.queue, item)
		return
	}

	if !p.loopSong {
		p.RemoveSongFromQueue(item)
	}
}

func (p *MusicPlayer) findSongIndex(videoID string) int {
	for i := range p.queue {
		if p.queue[i].VideoID == videoID {
			return i
		}
	}
	return -1
}
